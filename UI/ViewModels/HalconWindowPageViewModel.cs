﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Documents;
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
        public ObservableCollection<FaiItem> FaiItems { get; private set; }

        public ObservableCollection<FindLineParam> FindLineParams { get; private set; }

        public SnackbarMessageQueue SnackbarMessageQueue { get; set; } = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(500));

        private HWindow _windowHandle;
        
        public HObject DisplayImage { get; set; }

        private readonly IMeasurementProcedure MeasurementUnit = new I94TopViewMeasure("I94");

        public ICommand ExecuteCommand { get; }
        public ICommand ContinuousRunCommand { get; }

        public ICommand OpenLastDirectoryCommand { get;  }
        public string TimeElapsed { get; set; }

        public string ParamSerializationBaseDir
        {
            get { return SerializationDir + "/FindLineParams"; }
        }

        public int IndexToShow { get; set; } = 1;

        public async Task ProcessAsync(List<HImage> images)
        {
            HalconGraphics graphics = new HalconGraphics();
            Dictionary<string, double> results = new Dictionary<string, double>();
            var findLineConfigs = new FindLineConfigs(FindLineParams.ToList(), FindLineLocaionsRelativeValues);
            DataRecorder dataRecorder = null;
            
            await Task.Run(() =>
            {
                results =  MeasurementUnit.Process(images, findLineConfigs, FaiItems, IndexToShow, out graphics, out dataRecorder);
            });
            
            
            
            graphics.DisplayGraphics(_windowHandle);
            dataRecorder.DisplayPoints(_windowHandle);
            dataRecorder.Serialize(CsvDir + "/DebuggingData.csv");
            UpdateFaiItems(results);
            CsvSerializer.Serialize(FaiItems);
        }

        private void UpdateFaiItems(Dictionary<string,double> results)
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
            image.DispObj(_windowHandle);
            if (graphics.IsInitialized()) graphics.DispObj(_windowHandle);
        }


        /// <summary>
        /// Take measured values from halcon script and fill them into <see cref="FaiItems"/>
        /// </summary>
        /// <param name="measuredValues"></param>
        private void FillFaiItems(HTuple measuredValues)
        {
            var values = measuredValues.ToDArr();
            var numValues = values.Length;
            var numItems = FaiItems.Count;
            if (numItems != numValues)
                throw new InvalidOperationException($"Expect {numItems} but get {numValues} measured values");

            for (int i = 0; i < numValues; i++)
            {
                FaiItems[i].Value = values[i];
            }
        }

        public FaiItemCsvSerializer CsvSerializer { get; set; }

        public HalconWindowPageViewModel(HWindow windowHandle)
        {
            _windowHandle = windowHandle;
            
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
            FindLineLocaionsRelativeValues = MeasurementUnit.GenFindLineLocationValues();

             // Init commands
            ExecuteCommand = new RelayCommand(async () => { await ProcessOnceAsync(); });

            SelectImageDirCommand = new RelayCommand(() =>
            {
                
                using (var fbd = new FolderBrowserDialog())
                {
             
                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        ImageDirectory = fbd.SelectedPath;
                        LastDirectory = fbd.SelectedPath;
                    }
                }
            });

            ContinuousRunCommand = new RelayCommand(async () =>
            {
                while (!ImagesRunOut)
                {
                   await ProcessOnceAsync();
                }

                MultipleImagesRunning = false;
            });

            OpenLastDirectoryCommand = new RelayCommand(() => { ImageDirectory = LastDirectory; });
        }

        public string LastDirectory { get; set; }

        public List<FindLineLocation> FindLineLocaionsRelativeValues { get; set; }

        public bool MultipleImagesRunning { get; set; }

        private async Task ProcessOnceAsync()
        {
            if (ImageInputs == null) return;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            await ProcessAsync(ImageInputs);

            stopwatch.Stop();
            TimeElapsed = stopwatch.ElapsedMilliseconds.ToString();
        }


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
            get { return Application.StartupPath + "/" + MeasurementUnit.Name; }
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