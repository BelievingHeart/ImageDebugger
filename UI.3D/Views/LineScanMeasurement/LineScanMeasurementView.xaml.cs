﻿using System.Windows;
using System.Windows.Controls;
using ImageDebugger.Core.ViewModels.Base;
using ImageDebugger.Core.ViewModels.LineScan;

namespace UI._3D.Views.LineScanMeasurement
{
    public partial class LineScanMeasurementView : UserControl
    {
        public LineScanMeasurementView()
        {
            InitializeComponent();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            // Main window
            var dataContext = DataContext as LineScanMeasurementViewModel;
            dataContext.WindowHandle = HalconWindow.HalconWindow;
            dataContext.WindowHandle.SetPart(0,0,-2,-2);
            
        }
    }
}