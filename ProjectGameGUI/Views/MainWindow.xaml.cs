﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using Avalonia.Controls.Shapes;
using System.Threading.Tasks;
using Avalonia.Threading;
using System;
using ProjectGameGUI.ViewModels;
using ProjectGameGUI.Controls;
#if DEBUG
using Serilog;
using Avalonia.Logging.Serilog;
#endif

namespace ProjectGameGUI.Views
{
    public class MainWindow : Window
    {
        ExpandedGrid MainGrid;

        public MainWindow()
        {
            MainWindowViewModel viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            Initialized += MainWindow_Initialized;
            InitializeComponent();
#if DEBUG
            InitializeLogging();
            this.AttachDevTools();
#endif
            MainGrid = this.Get<ExpandedGrid>("MainGrid");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeLogging()
        {
#if DEBUG
            SerilogLogger.Initialize(new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Trace(outputTemplate: "{Area}: {Message}")
            .CreateLogger());
#endif
        }

        private void MainWindow_Initialized(object sender, EventArgs e)
        {
            ((MainWindowViewModel)DataContext).InitializeGame();
        }
    }
}
