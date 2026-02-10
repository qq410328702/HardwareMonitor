using HardwareMonitor.Services;
using HardwareMonitor.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace HardwareMonitor
{
    public partial class App : Application
    {
        private MainViewModel? _vm;
        private MainWindow? _mainWindow;
        private FileLogger? _logger;

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Apply light theme as default
            ThemeService.Apply(1);

            // Create logger and hardware service with dependency injection
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HardwareMonitor", "logs");
            _logger = new FileLogger(logDir);
            var hwService = new HardwareService(_logger);

            // Create shared ViewModel with injected dependency
            _vm = new MainViewModel(hwService);

            // Create main window but keep it hidden
            _mainWindow = new MainWindow(_vm);

            // Show mini window directly
            ShowMini();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _vm?.Dispose();
            }
            finally
            {
                try
                {
                    _logger?.Dispose();
                }
                finally
                {
                    base.OnExit(e);
                }
            }
        }

        public void ShowMini()
        {
            _mainWindow?.Hide();
            var mini = new MiniWindow(_vm!);
            mini.ExpandRequested += (_, _) => ShowMain();
            mini.Closed += (_, _) =>
            {
                if (_mainWindow is null || !_mainWindow.IsVisible)
                {
                    Shutdown();
                }
            };
            mini.Show();
        }


        public void ShowMain()
        {
            if (_mainWindow is not null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }
    }
}
