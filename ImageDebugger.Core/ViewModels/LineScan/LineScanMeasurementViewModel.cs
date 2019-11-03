﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using cyXYZInspector;
using HalconDotNet;
using ImageDebugger.Core.Commands;
using ImageDebugger.Core.Enums;
using ImageDebugger.Core.Helpers;
using ImageDebugger.Core.ImageProcessing.LineScan;
using ImageDebugger.Core.ImageProcessing.LineScan.Procedure;
using ImageDebugger.Core.ImageProcessing.Utilts;
using ImageDebugger.Core.ViewModels.Application;
using ImageDebugger.Core.ViewModels.Base;
using ImageDebugger.Core.ViewModels.LineScan.Flatness;
using ImageDebugger.Core.ViewModels.LineScan.Parallelism;
using ImageDebugger.Core.ViewModels.LineScan.PointSetting;
using ImageDebugger.Core.ViewModels.LineScan.Thickness;

namespace ImageDebugger.Core.ViewModels.LineScan
{
    public class LineScanMeasurementViewModel : MeasurementPlayerViewModelBase
    {
        private bool _currentViewIsBackView;
        private ILineScanMeasurementProcedure LineScanMeasurementProcedure { get; set; } = new  I40LineScanMeasurement();

        /// <summary>
        /// Point settings
        /// </summary>
        public List<PointSettingViewModel> PointSettingViewModels { get; set; }

        public string ProcedureName
        {
            get { return LineScanMeasurementProcedure.Name; }
        }

        private Dictionary<string, Plane3D> Planes { get; set; } = new Dictionary<string, Plane3D>();
 
        public HWindow WindowHandleBottomRight { get; set; }
        
        public HWindow WindowHandleLeftRight { get; set; }
        public List<FlatnessItemViewModel> FlatnessViewModels { get; set; }


        public List<ParallelismItemViewModel> ParallelismItemViewModels { get; set; }
        
        public List<ThicknessItemViewModel> ThicknessViewModels { get; set; }


        /// <summary>
        /// Specifies whether the currently showing image is back-view or front-view
        /// </summary>
        public bool CurrentViewIsBackView
        {
            get { return _currentViewIsBackView; }
            set
            {
                _currentViewIsBackView = value;
                if (_currentViewIsBackView) ToggleBackView();
                else ToggleFrontView();
            }
        }

        private void ToggleFrontView()
        {
            WindowHandle.DispColor(FrontView);
            Result.Display(WindowHandle);
        }

        private void ToggleBackView()
        {
            WindowHandle.DispColor(BackView);
            Result.Display(WindowHandle);
        }


        private Dictionary<string, string> ThicknessPointPlaneMatches { get; set; } = new Dictionary<string, string>()
        {
            ["19-A1"] = "19-F",
            ["19-A2"] = "19-F",
            ["19-A3"] = "19-F",
            ["19-A4"] = "19-F",
            ["19-A5"] = "19-F",
            ["19-A6"] = "19-F",
            ["19-A7"] = "19-F",
            ["19-A8"] = "19-F",
        };
        
        private List<string> ThicknessPointPointMatches { get; set; } = new List<string>()
        {
            "17.1", "17.2", "17.3", "17.4"
        };
        
        private List<string> FlatnessNames { get; set; } = new List<string>()
        {
            "16.3", "16.5"
        };

        private List<string> PlaneNames { get; set; } = new List<string>()
        {
                "16.3", "16.5","19-F"
        };
        
        /// <summary>
        /// Root directory for serialization
        /// </summary>
        private string ConfigurationBaseDir
        {
            get { return Path.Combine(ApplicationViewModel.SolutionDirectory + "/Configs/3D/", LineScanMeasurementProcedure.Name); }
        }

        private static string CsvDir
        {
            get { return Directory.GetCurrentDirectory() + "/CSV"; }
        }

        /// <summary>
        /// Serialize measurement results
        /// </summary>
        private CsvSerializer CsvSerializer { get; }
        
        /// <summary>
        /// The content to show in the drawer that sits on the right
        /// </summary>
        public DrawerContentType3D DrawerContent { get; set; } = DrawerContentType3D.PointSettings;

        private string PointSettingSerializationDir
        {
            get { return Path.Combine(ConfigurationBaseDir, "Points"); }
        }

        protected override int NumImagesInOneGoRequired
        {
            get { return LineScanMeasurementProcedure.NumImageRequireInSingleRun; }
        }

        public LineScanMeasurementViewModel()
        {
            PointSettingViewModels =
                AutoSerializableHelper.LoadAutoSerializables<PointSettingViewModel>(
                    LineScanMeasurementProcedure.PointNames, PointSettingSerializationDir).ToList(); 
            
            ImageProcessStartAsync += OnImageProcessStartAsync;
            
            // Commands
            ShowPointSettingViewCommand = new RelayCommand(ShowPointSettingView);
            ShowFlatnessViewCommand = new RelayCommand(ShowFlatnessView);
            ShowParallelismViewCommand = new RelayCommand(ShowParallelismView);
            ShowThicknessViewCommand = new RelayCommand(ShowThicknessView);
            OpenCsvDirCommand = new RelayCommand(OpenCsvDir);
            
            CsvSerializer = new CsvSerializer(CsvDir);
            ContinuousModeFinished += CsvSerializer.SummariseCsv;
        }

        private void OpenCsvDir()
        {
            if(Directory.Exists(CsvDir)) Process.Start(CsvDir);
        }

        private async Task OnImageProcessStartAsync(List<HImage> images)
        {
            var result = await Task.Run(() => LineScanMeasurementProcedure.Process(ImageInputs, PointSettingViewModels, RunStatusMessageQueue));

            var imageDisplay = result.Images[0];
            InfoImage = imageDisplay;

            BackView = result.Images[1];
            FrontView = result.Images[2];
            Result = result;
            if (CurrentViewIsBackView) ToggleBackView();
            else ToggleFrontView();
            
            // Calculate results
            UpdatePointSettings(result.PointMarkers);
            ConstructPlanes(PointSettingViewModels);
            CalcFlatness();
            CalcThickness();
            
            // Serialize
            var csvSerializables = new List<ICsvColumnElement>();
            csvSerializables.AddRange(PointSettingViewModels);
            csvSerializables.AddRange(FlatnessViewModels);
            csvSerializables.AddRange(ThicknessViewModels);
            CsvSerializer.Serialize(csvSerializables, CurrentImageName, IsContinuouslyRunning);
        }

        private ImageProcessingResults3D Result { get; set; }

        private HImage FrontView { get; set; }

        private HImage BackView { get; set; }

        private void CalcThickness()
        {
            var output = new List<ThicknessItemViewModel>();
            
            // Point-point thickness
            foreach (var name in ThicknessPointPointMatches)
            {
                var pointPair = PointSettingViewModels.Where(p => p.Name.StartsWith(name)).ToList();
                Trace.Assert(pointPair.Count == 2, $"Expected 2 points but get {pointPair.Count}");
            
                
                output.Add(new ThicknessItemViewModel()
                {
                    Name = name,
                    Value = Math.Abs(pointPair[0].Value - pointPair[1].Value)
                });
            }
            
            // Point-plane thickness
            foreach (var match in ThicknessPointPlaneMatches)
            {
                var point = PointSettingViewModels.ByName(match.Key);
                var plane = Planes[match.Value];
                output.Add(new ThicknessItemViewModel()
                {
                    Name = point.Name,
                    Value = plane.GetDistance(point.X, point.Y, point.Value)
                });
            }
            
            ThicknessViewModels = output;
        }

      


        private void CalcFlatness()
        {
            var output = new List<FlatnessItemViewModel>();
            foreach (var name in FlatnessNames)
            {
                var plane = Planes[name];
                var planeData = GrabPlaneData(PointSettingViewModels, name);
                output.Add(new FlatnessItemViewModel()
                {
                    Name = name,
                    Value = plane.MeasureFlatness(planeData.Select(d => d.X).ToArray(),
                        planeData.Select(d => d.Y).ToArray(), planeData.Select(d => d.Value).ToArray())
                });
            }

            FlatnessViewModels = output;
        }


        private void ConstructPlanes(List<PointSettingViewModel> pointSettings)
        {
            foreach (var planeName in PlaneNames)
            {
                IEnumerable<PointSettingViewModel> planeData = GrabPlaneData(pointSettings, planeName);
                Planes[planeName] = Plane3D.leastSquareAdaptFlatSurface(planeData.Select(d => d.X).ToArray(),
                    planeData.Select(d => d.Y).ToArray(), planeData.Select(d => d.Value).ToArray());
            }
        }


        private IEnumerable<PointSettingViewModel> GrabPlaneData(List<PointSettingViewModel> pointSettings, string planeName)
        {
            return pointSettings.Where(p => p.Name.StartsWith(planeName));
        }

        private void UpdatePointSettings(List<PointMarker> pointMarkers)
        {
            PointSettingViewModels.StopAutoSerializing();

            foreach (var pointMarker in pointMarkers)
            {
                PointSettingViewModels.ByName(pointMarker.Name).Value = pointMarker.Height;
            }            
            
            PointSettingViewModels.StartAutoSerializing();
        }

        private void ShowThicknessView()
        {
            DrawerContent = DrawerContentType3D.Thickness;
        }

        private void ShowParallelismView()
        {
            DrawerContent = DrawerContentType3D.Parallelism;
        }

        private void ShowFlatnessView()
        {
            DrawerContent = DrawerContentType3D.Flatness;
        }

        private void ShowPointSettingView()
        {
            DrawerContent = DrawerContentType3D.PointSettings;
        }


        public ICommand ShowPointSettingViewCommand { get; private set; }
        public ICommand ShowFlatnessViewCommand { get; private set; }
        public ICommand ShowParallelismViewCommand { get; private set; }
        public ICommand ShowThicknessViewCommand { get; private set; }

        public ICommand OpenCsvDirCommand { get; private set; }
    }
}