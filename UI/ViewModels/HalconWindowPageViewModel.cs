﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Serialization;
using HalconDotNet;
using MaterialDesignThemes.Wpf;
using UI.Commands;
using UI.ImageProcessing;
using UI.ImageProcessing.Utilts;
using UI.Model;


namespace UI.ViewModels
{
    public partial class HalconWindowPageViewModel : ViewModelBase
    {
        private IMeasurementProcedure _measurementUnit;
        public ObservableCollection<FaiItem> FaiItems { get; private set; }

        public ObservableCollection<FindLineParam> FindLineParams { get; private set; }

        public SnackbarMessageQueue RunStatusMessageQueue { get; set; } = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(5000));

        public string ProcedureName => MeasurementUnit.Name;
        
        public HWindow WindowHandle { get; set; }

        public HObject DisplayImage { get; set; }

        public List<string> ImageNames { get; set; }

        public IMeasurementProcedure MeasurementUnit
        {
            get => _measurementUnit;
            set
            {
                _measurementUnit = value;
                CsvSerializer = new FaiItemCsvSerializer(CsvDir);
                ReloadFindlineConfigurations();
            }
        }


        public ICommand RunNextCommand { get; }

        public ICommand RunPreviousCommand { get; }

        public ICommand ContinuousRunCommand { get; }

        public string TimeElapsed { get; set; }

        public bool SystemIsBusy { get; set; }

        public string ParamSerializationBaseDir
        {
            get { return SerializationDir + "/FindLineParams"; }
        }

        public int IndexToShow { get; set; } = 1;

        private void UpdateFaiItems(Dictionary<string, double> results)
        {
            FaiItemsStopListeningToChange();

            foreach (var item in FaiItems)
            {
                item.Value = results[item.Name];
            }

            FaiItemsRestartListeningToChange();
        }

        public string CsvDir
        {
            get { return SerializationDir + "/CSV"; }
        }

        private void ShowImageAndGraphics(HImage image, HObject graphics)
        {
//            HOperatorSet.ClearWindow(_windowHandle);
            image.DispObj(WindowHandle);
            if (graphics.IsInitialized()) graphics.DispObj(WindowHandle);
        }
        

        public List<int> ImageToShowSelectionList { get; private set; } = new List<int>();

        public FaiItemCsvSerializer CsvSerializer { get; set; }


        public HalconWindowPageViewModel(HWindow windowHandle, IMeasurementProcedure procedure)
        {
            WindowHandle = windowHandle;
            MeasurementUnit = procedure;
        }

        public HalconWindowPageViewModel()
        {

            RunNextCommand = new RelayCommand(async () =>
            {
                if (SystemIsBusy) return;
                //Note: Do not combine these two lines
                // because Current image index will adjusts itself
                CurrentImageIndex++;
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await ProcessOnceAsync(GrabImageInputs(CurrentImageIndex)); });
            });
            RunPreviousCommand = new RelayCommand(async () =>
            {
                if (SystemIsBusy) return;
                CurrentImageIndex--;
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await ProcessOnceAsync(GrabImageInputs(CurrentImageIndex)); });
            });

            SelectImageDirCommand = new RelayCommand(() =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        ImageDirectory = fbd.SelectedPath;
                    }
                }
            });

            ContinuousRunCommand = new RelayCommand(async () =>
            {
                if (SystemIsBusy) return;

                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy, async () =>
                    {
                        while (MultipleImagesRunning)
                        {
                            var lastIndex = CurrentImageIndex;
                            CurrentImageIndex++;
                            if (lastIndex == TotalImages - 1)
                            {
                                _currentImageIndex = -1;
                                OnPropertyChanged(nameof(CurrentImageIndex));
                                OnPropertyChanged(nameof(CurrentImageName));
                                break;
                            }

                            await ProcessOnceAsync(GrabImageInputs(CurrentImageIndex));
                        }
                    })
                    ;

                MultipleImagesRunning = false;
            });

            ImageNameSelectionChangedCommand = new RelayCommand(async () =>
            {
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await ProcessOnceAsync(GrabImageInputs(CurrentImageIndex)); });
            });
        }

        private void ReloadFindlineConfigurations()
        {
// Init fai items
            var faiItemsFromDisk = TryLoadFaiItemsFromDisk();
            FaiItems = faiItemsFromDisk ?? MeasurementUnit.GenFaiItemValues(FaiItemSerializationDir);
            foreach (var item in FaiItems)
            {
                item.ResumeAutoSerialization();
            }

            // Init find line params
            var findLineParamsFromDisk = TryLoadFindLineParamsFromDisk();
            FindLineParams = findLineParamsFromDisk ??
                             MeasurementUnit.GenFindLineParamValues(ParamSerializationBaseDir);

            foreach (var param in FindLineParams)
            {
                param.ResumeAutoSerialization();
            }

            // Init find line locations
            FindLineLocationsRelativeValues = MeasurementUnit.GenFindLineLocationValues();
        }


        public string LastDirectory { get; set; }

        public List<FindLineLocation> FindLineLocationsRelativeValues { get; set; }

        public async Task ProcessAsync(List<HImage> images)
        {
            var findLineConfigs = new FindLineConfigs(FindLineParams.ToList(), FindLineLocationsRelativeValues);

            var result =
                await Task.Run(() =>
                    MeasurementUnit.ProcessAsync(images, findLineConfigs, FaiItems, IndexToShow,
                        RunStatusMessageQueue));


            if (WindowHandle!=null)
            {
                result.HalconGraphics.DisplayGraphics(WindowHandle);
                result.DataRecorder.DisplayPoints(WindowHandle);
            }
            result.DataRecorder.Serialize(CsvDir + "/DebuggingData.csv");
            UpdateFaiItems(result.FaiDictionary);
            CsvSerializer.Serialize(FaiItems, ImageNames[CurrentImageIndex]);
        }

        private async Task ProcessOnceAsync(List<HImage> images)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            await ProcessAsync(images);

            stopwatch.Stop();
            TimeElapsed = stopwatch.ElapsedMilliseconds.ToString();
        }

        public bool MultipleImagesRunning { get; set; }


        private void FaiItemsRestartListeningToChange()
        {
            foreach (var item in FaiItems)
            {
                item.ResumeAutoSerialization();
            }
        }

        private void FaiItemsStopListeningToChange()
        {
            foreach (var item in FaiItems)
            {
                item.StopAutoSerialization();
            }
        }

        public string SerializationDir
        {
            get { return Application.StartupPath + "/" + ProcedureName; }
        }

        public string FaiItemSerializationDir
        {
            get { return SerializationDir + "/FaiItems"; }
        }


        private ObservableCollection<FaiItem> TryLoadFaiItemsFromDisk()
        {
            var directoryInfo = Directory.CreateDirectory(FaiItemSerializationDir);
            var xmls = directoryInfo.GetFiles("*.xml");
            if (xmls.Length == 0) return null;

            var outputs = new ObservableCollection<FaiItem>();
            foreach (var fileInfo in xmls)
            {
                string filePath = fileInfo.FullName;
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var serializer = new XmlSerializer(typeof(FaiItem));
                    FaiItem item = (FaiItem) serializer.Deserialize(fs);
                    item.SerializationDir = FaiItemSerializationDir;
                    outputs.Add(item);
                }
            }

            foreach (var item in outputs)
            {
                item.ResumeAutoSerialization();
            }

            return outputs;
        }


        private ObservableCollection<FindLineParam> TryLoadFindLineParamsFromDisk()
        {
            var directoryInfo = Directory.CreateDirectory(ParamSerializationBaseDir);
            var xmls = directoryInfo.GetFiles("*.xml");
            if (xmls.Length == 0) return null;

            var outputs = new ObservableCollection<FindLineParam>();
            foreach (var fileInfo in xmls)
            {
                string filePath = fileInfo.FullName;
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var serializer = new XmlSerializer(typeof(FindLineParam));
                    FindLineParam item = (FindLineParam) serializer.Deserialize(fs);
                    item.SerializationDir = ParamSerializationBaseDir;
                    outputs.Add(item);
                }
            }

            foreach (var item in outputs)
            {
                item.ResumeAutoSerialization();
            }

            return outputs;
        }
    }
}