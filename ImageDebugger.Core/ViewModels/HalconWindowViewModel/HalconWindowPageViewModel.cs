﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using HalconDotNet;
using ImageDebugger.Core.Commands;
using ImageDebugger.Core.ImageProcessing;
using ImageDebugger.Core.ImageProcessing.Utilts;
using ImageDebugger.Core.IoC.Interface;
using ImageDebugger.Core.Models;
using MaterialDesignThemes.Wpf;

namespace ImageDebugger.Core.ViewModels.HalconWindowViewModel
{
    public partial class HalconWindowPageViewModel : RecyclableMegaList<string>
    {
        /// <summary>
        /// The measurement procedure class that image processing is going to happen
        /// </summary>
        private IMeasurementProcedure _measurementUnit;

        /// <summary>
        /// The fai item list for displaying result to the UI
        /// </summary>
        public ObservableCollection<FaiItem> FaiItems { get; private set; }

        /// <summary>
        /// The find line parameters to display for editing
        /// and will be apply to tweak haw the lines will be found
        /// </summary>
        public ObservableCollection<FindLineParam> FindLineParams { get; private set; }

        /// <summary>
        /// The message queue for outputting debugging information to user
        /// </summary>
        public SnackbarMessageQueue RunStatusMessageQueue { get; set; } =
            new SnackbarMessageQueue(TimeSpan.FromMilliseconds(5000));

        /// <summary>
        /// Name of the image processing procedure
        /// </summary>
        public string ProcedureName => MeasurementUnit == null ? "" : MeasurementUnit.Name;

        /// <summary>
        /// The window handle to display debugging graphics to the halcon window
        /// </summary>
        public HWindow WindowHandle { get; set; }

        /// <summary>
        /// The image to display in MVVM style
        /// </summary>
        public HObject DisplayImage { get; set; }

        /// <summary>
        /// The image names for the combo box to run selected image to choose from
        /// </summary>
        public List<string> ImageNames { get; set; }


        /// <summary>
        /// The measurement procedure that image processing happens
        /// </summary>
        public IMeasurementProcedure MeasurementUnit
        {
            get { return _measurementUnit; }
            set
            {
                _measurementUnit = value;
                CsvSerializer = new FaiItemCsvSerializer(CsvDir);
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
        }

        /// <summary>
        /// The command to run next batch of images
        /// </summary>
        public ICommand RunNextCommand { get; }

        /// <summary>
        /// The command to run previous batch of images
        /// </summary>
        public ICommand RunPreviousCommand { get; }

        /// <summary>
        /// The command to continuously run all the images from the current index
        /// </summary>
        public ICommand ContinuousRunCommand { get; }

        /// <summary>
        /// Command to display X,Y and gray value information of the current shown image
        /// </summary>
        public ICommand DisplayXYGrayCommand { get; set; }

        /// <summary>
        /// Whether the image info should be displayed
        /// </summary>
        public bool ShouldImageInfoDisplay { get; set; }

        /// <summary>
        /// Display current gray value and image x, y coordinates
        /// </summary>
        /// <param name="o"></param>
        private void DisplayXYGray(object o)
        {
            double grayvalue = -1;
            int imageX = -1, imageY = -1;
            ShouldImageInfoDisplay = true;
            HSmartWindowControlWPF.HMouseEventArgsWPF e = (HSmartWindowControlWPF.HMouseEventArgsWPF) o;
            try
            {
                imageX = (int) e.Column;
                imageY = (int) e.Row;
                grayvalue = (DisplayImage as HImage)?.GetGrayval(imageY, imageX);
            }
            catch (Exception exception)
            {
                ShouldImageInfoDisplay = false;
            }

            // output grayvalue info
            GrayValueInfo = new ImageInfoViewModel()
            {
                X = imageX, Y = imageY, GrayValue = (int) grayvalue
            };

            // Set the pop-up position
            MouseX = (int) e.X + 20;
            MouseY = (int) e.Y + 20;
        }


        /// <summary>
        /// Time span measured for each run of image processing
        /// </summary>
        public string TimeElapsed { get; set; }

        /// <summary>
        /// Specifies whether image processing is running
        /// </summary>
        public bool SystemIsBusy { get; set; }

        /// <summary>
        /// Gray value information of the current image point under the cursor
        /// </summary>
        public ImageInfoViewModel GrayValueInfo { get; set; }

        /// <summary>
        /// The directory for outputting find-line parameter config files
        /// </summary>
        public string ParamSerializationBaseDir
        {
            get { return SerializationDir + "/FindLineParams"; }
        }

        /// <summary>
        /// The index within the input batch of images to show
        /// </summary>
        public int IndexToShow { get; set; } = 0;

        public int MouseX { get; set; }

        public int MouseY { get; set; }
        
  

        /// <summary>
        /// Update fai items to display after image processing
        /// </summary>
        /// <param name="results">Results return from image processing</param>
        private void UpdateFaiItems(Dictionary<string, double> results)
        {
            FaiItemsStopListeningToChange();

            foreach (var item in FaiItems)
            {
                item.Value = results[item.Name];
            }

            FaiItemsRestartListeningToChange();
        }

        /// <summary>
        /// The directory to output measurement values
        /// as well as some debugging information specific to image processing 
        /// </summary>
        public string CsvDir
        {
            get { return SerializationDir + "/CSV"; }
        }

        /// <summary>
        /// The underlying list for the UI the choose which image within the input batch
        /// should be shown
        /// </summary>
        public List<int> ImageToShowSelectionList { get; private set; } = new List<int>();

        /// <summary>
        /// The serializer that manages all the serialization logic for serializing fai items
        /// </summary>
        public FaiItemCsvSerializer CsvSerializer { get; set; }


        /// <summary>
        /// Default constructor
        /// </summary>
        public HalconWindowPageViewModel()
        {
            // Update current image name to show
            CurrentIndexChanged += index => { CurrentImageName = index < 0 ? "" : ImageNames[index]; };

            // Prompt user when the current index overflows
            UpperIndexExceeded += () => PromptUserThreadSafe("Reached the end of list, start over");
            // Prompt user when the current index jumps to end of list
            LowerIndexExceeded += () => PromptUserThreadSafe("Jump to the end of image list");

            RunNextCommand = new SimpleCommand(async o =>
            {
                if (SystemIsBusy) return;
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await TimedProcessAsync(ConvertPathsToImages(NextUnbounded())); });
            }, o => HasImages);

            RunPreviousCommand = new SimpleCommand(async o =>
            {
                if (SystemIsBusy) return;
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await TimedProcessAsync(ConvertPathsToImages(PreviousUnbounded())); });
            }, o => HasImages);

            SelectImageDirCommand = new SimpleCommand(o => { ImagePaths = IoC.IoC.Get<IImageProvider>().GetImages(); },
                o => !SystemIsBusy);

            ContinuousRunCommand = new SimpleCommand(async o =>
            {
                if (SystemIsBusy) return;

                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy, async () =>
                    {
                        while (IsContinuouslyRunning)
                        {
                            var inputPaths = NextBounded();
                            if (inputPaths == null) break;

                            await TimedProcessAsync(ConvertPathsToImages(inputPaths));
                        }
                    })
                    ;

                IsContinuouslyRunning = false;
            }, o => HasImages);

            ImageNameSelectionChangedCommand = new SimpleCommand(async o =>
            {
                int index = (int) o;
                await RunOnlySingleFireIsAllowedEachTimeCommand(() => SystemIsBusy,
                    async () => { await TimedProcessAsync(ConvertPathsToImages(JumpTo(index))); });
            }, o => HasImages);
            
            DisplayXYGrayCommand = new SimpleCommand(DisplayXYGray, o=>DisplayImage!=null);
        }

        /// <summary>
        /// Convert image paths to <see cref="HImage"/>
        /// </summary>
        /// <param name="imagePaths">Paths to images</param>
        /// <returns></returns>
        private List<HImage> ConvertPathsToImages(List<string> imagePaths)
        {
            var output = new List<HImage>();
            foreach (var path in imagePaths)
            {
                output.Add(new HImage(path));
            }

            return output;
        }


        /// <summary>
        /// Find line locations that are relative to the selected origin
        /// These locations will not change once from run to run
        /// </summary>
        public List<FindLineLocation> FindLineLocationsRelativeValues { get; set; }

        /// <summary>
        /// Process a batch of images and handle feedback
        /// </summary>
        /// <param name="images">A batch of input images</param>
        /// <returns></returns>
        private async Task ProcessAsync(List<HImage> images)
        {
            var findLineConfigs = new FindLineConfigs(FindLineParams.ToList(), FindLineLocationsRelativeValues);

            var result =
                await Task.Run(() =>
                    MeasurementUnit.ProcessAsync(images, findLineConfigs, FaiItems, IndexToShow,
                        RunStatusMessageQueue));
            
            DisplayImage = images[IndexToShow];
            images[IndexToShow].DispImage(WindowHandle);

            if (WindowHandle != null)
            {
                result.HalconGraphics.DisplayGraphics(WindowHandle);
                result.DataRecorder.DisplayPoints(WindowHandle);
            }

            result.DataRecorder.Serialize(CsvDir + "/DebuggingData.csv");
            UpdateFaiItems(result.FaiDictionary);
            CsvSerializer.Serialize(FaiItems, ImageNames[CurrentIndex]);
        }

        /// <summary>
        /// Do timing for a single image process run
        /// </summary>
        /// <param name="images">A batch of input images</param>
        /// <returns></returns>
        private async Task TimedProcessAsync(List<HImage> images)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            await ProcessAsync(images);

            stopwatch.Stop();
            TimeElapsed = stopwatch.ElapsedMilliseconds.ToString();
        }

        /// <summary>
        /// Specifies whether image processing is continuously running
        /// </summary>
        public bool IsContinuouslyRunning { get; set; }

        /// <summary>
        /// All the fai items will automatically serialize themselves once changed after this method call
        /// </summary>
        private void FaiItemsRestartListeningToChange()
        {
            foreach (var item in FaiItems)
            {
                item.ResumeAutoSerialization();
            }
        }

        /// <summary>
        /// All the fai items will stop auto-serialization after this method call
        /// </summary>
        private void FaiItemsStopListeningToChange()
        {
            foreach (var item in FaiItems)
            {
                item.StopAutoSerialization();
            }
        }

        /// <summary>
        /// The base directory for serializing everything
        /// </summary>
        public string SerializationDir =>
            IoC.IoC.Get<ISerializationManager>().SerializationBaseDir + "/" + ProcedureName;

        /// <summary>
        /// The directory to serialize fai item settings
        /// </summary>
        public string FaiItemSerializationDir
        {
            get { return SerializationDir + "/FaiItems"; }
        }

        /// <summary>
        /// Try to load fai item setting from disk if any
        /// </summary>
        /// <returns>The loaded fai items</returns>
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

        /// <summary>
        /// Try to load find-line parameters from disk if any
        /// </summary>
        /// <returns>The loaded find-line parameters</returns>
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