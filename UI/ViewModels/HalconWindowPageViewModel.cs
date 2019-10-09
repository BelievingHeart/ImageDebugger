﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Serialization;
using HalconDotNet;
using MathNet.Numerics.LinearAlgebra;
using UI.Commands;
using UI.ImageProcessing;
using UI.ImageProcessing.Utilts;

// TODO: add faiitem serialize logic
namespace UI.ViewModels
{
    public partial class HalconWindowPageViewModel : ViewModelBase
    {
        public ObservableCollection<FaiItem> FaiItems { get; private set; }

        public ObservableCollection<FindLineParam> FindLineParams { get; private set; }

        private HWindow _windowHandle;

        private FindLineConfigs _findLineConfigs;

        public HObject DisplayImage { get; set; }

        private readonly IMeasurementProcedure MeasurementUnit = new I94TopViewMeasure("I94");

        public ICommand ExecuteCommand { get; }
        public ICommand ContinuousRunCommand { get; }

        public string ParamSerializationBaseDir => SerializationDir + "/FindLineParams";


        public void Process(List<HImage> images)
        {
            MeasurementUnit.Process(images, _findLineConfigs, _windowHandle, FaiItems);
            CsvSerializer.Serialize(FaiItems);
        }

        public string CsvDir => SerializationDir + "/CSV";
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
            FaiItems = faiItemsFromDisk ?? FaiItemHardCodeValues();
            foreach (var item in FaiItems)
            {
                item.ResumeAutoSerialization();
            }

            // Init find line params
            var findLineParamsFromDisk = TryLoadFindLineParamsFromDisk();
            FindLineParams = findLineParamsFromDisk ?? FindLineParamsHardCodeValues();
            foreach (var param in FindLineParams)
            {
                param.ResumeAutoSerialization();
            }

            // Init find line locations
            var findLineLocations = FindLineLocationHardCodeValues();
            _findLineConfigs = new FindLineConfigs(FindLineParams.ToList(), findLineLocations);

            // Listen for user changes of data grid
            MeasurementUnit.MeasurementResultReady += FaiItemsStopListeningToChange;
            MeasurementUnit.MeasurementResultPulled += FaiItemsRestartListeningToChange;


            // Init commands
            ExecuteCommand = new RelayCommand(() => { ProcessOnce(); });

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

            ContinuousRunCommand = new RelayCommand(() =>
            {
                while (RunContinuously)
                {
                    ProcessOnce();
                }
            });
        }

        private void ProcessOnce()
        {
            var inputs = ImageInputs;
            if (inputs == null) return;

            Process(inputs);
        }

        public bool RunContinuously { get; set; } = false;

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

        public string SerializationDir => Application.StartupPath + "/" + MeasurementUnit.Name;

        public string FaiItemSerializationDir => SerializationDir + "/FaiItems";


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