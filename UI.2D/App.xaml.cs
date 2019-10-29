﻿using System.Windows;
using ImageDebugger.Core.IoC;
using ImageDebugger.Core.IoC.Interface;
using UI._2D.DataAccess;

namespace UI._2D
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Set up IoC
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up IoC
            IoC.Kernel.Bind<IImageProvider>().ToConstant(new ImageProvider());
            IoC.Setup();
            
            // Open main window
            var window = new MainWindow();
            Current.MainWindow = window;
            window.Show();
        }
    }
}