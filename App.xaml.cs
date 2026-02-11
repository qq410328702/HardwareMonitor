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
        private MiniWindow? _miniWindow;
        private FileLogger? _logger;
        private ITrayService? _trayService;
        private IDataStorageService? _dataStorageService;
        private LayoutViewModel? _layoutVm;

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

            // Create new monitoring services
            var diskService = new DiskMonitorService(hwService, _logger);
            var networkService = new NetworkMonitorService(_logger);
            var processService = new ProcessMonitorService(_logger);

            // Create alert engine with default 60-second cooldown
            var alertEngine = new AlertEngine();

            // Create data storage service for persisting snapshots
            _dataStorageService = new DataStorageService();

            // Initialize system tray service
            _trayService = new TrayService(_logger);
            _trayService.Initialize();
            _trayService.ShowTrayIcon();

            // Create shared ViewModel with injected dependency
            _vm = new MainViewModel(hwService, diskService, networkService, processService, alertEngine, _trayService, _dataStorageService);

            // Create layout persistence and ViewModel
            var layoutService = new LayoutPersistenceService();
            _layoutVm = new LayoutViewModel(layoutService);

            // Create main window but keep it hidden
            _mainWindow = new MainWindow(_vm);
            _mainWindow.SetLayoutViewModel(_layoutVm);

            // Bind tray service events
            _trayService.ShowMainRequested += (_, _) => Dispatcher.Invoke(ShowMain);
            _trayService.ShowMiniRequested += (_, _) => Dispatcher.Invoke(ShowMini);
            _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);

            // Show mini window directly
            ShowMini();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _trayService?.Dispose();
            }
            finally
            {
                try
                {
                    _vm?.Dispose();
                }
                finally
                {
                    try
                    {
                        _dataStorageService?.Dispose();
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
            }
        }

        public void ShowMini()
        {
            _mainWindow?.Hide();

            if (_miniWindow is null || !_miniWindow.IsLoaded)
            {
                _miniWindow = new MiniWindow(_vm!);
                _miniWindow.ExpandRequested += (_, _) => ShowMain();
            }

            _miniWindow.Show();
            _miniWindow.Activate();
        }

        public void ShowMain()
        {
            _miniWindow?.Hide();

            if (_mainWindow is not null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            _trayService?.Dispose();
            Shutdown();
        }
    }
}
