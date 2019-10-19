﻿using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using UI.Commands;
using UI.Enums;

namespace UI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        /// <summary>
        /// Command to switch to bottom view page
        /// </summary>
        public ICommand SwitchBottomViewCommand { get; set; }

        /// <summary>
        /// Command to switch to top view page
        /// </summary>
        public ICommand SwitchTopViewCommand { get; set; }

        /// <summary>
        /// Current measurement page to show
        /// </summary>
        public MeasurementPage CurrentMeasurementPage { get; set; } = MeasurementPage.I94Top;

        public string CurrentMeasurementName => CurrentMeasurementPage.ToString();

        public MainWindowViewModel()
        {
            SwitchTopViewCommand = new RelayCommand(() => { CurrentMeasurementPage = MeasurementPage.I94Top;});
            SwitchBottomViewCommand = new RelayCommand(() => { CurrentMeasurementPage = MeasurementPage.I94Bottom; });
        }
        
    }
}