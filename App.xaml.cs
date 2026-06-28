using HardwareMonitor.Services;
using HardwareMonitor.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;
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
        private UpdateService? _updateService;

        private void App_Startup(object sender, StartupEventArgs e)
        {
            if (UpdateService.TryApplyUpdateFromArgs(e.Args))
            {
                Shutdown();
                return;
            }

            // Apply light theme as default
            ThemeService.Apply(1);

            // Create logger and hardware service with dependency injection
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HardwareMonitor", "logs");
            _logger = new FileLogger(logDir);
            var hwService = new HardwareService(_logger);
            _updateService = new UpdateService(_logger);

            // Create new monitoring services
            var diskService = new DiskMonitorService(hwService, _logger);
            var electricityCostService = new ElectricityCostService();

            // Initialize system tray service
            _trayService = new TrayService(_logger);
            _trayService.Initialize();
            _trayService.ShowTrayIcon();

            // Create shared ViewModel with injected dependency
            _vm = new MainViewModel(hwService, diskService, electricityCostService);

            // Create main window but keep it hidden
            _mainWindow = new MainWindow(_vm, _updateService);

            // Bind tray service events
            _trayService.ShowMainRequested += (_, _) => Dispatcher.Invoke(ShowMain);
            _trayService.ShowMiniRequested += (_, _) => Dispatcher.Invoke(ShowMini);
            _trayService.CheckUpdateRequested += (_, _) => Dispatcher.Invoke(CheckUpdatesFromTray);
            _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);

            // Show mini window directly
            ShowMini();
            _ = CheckForUpdatesOnStartupAsync();
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
                        _logger?.Dispose();
                    }
                    finally
                    {
                        base.OnExit(e);
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

        private void CheckUpdatesFromTray()
        {
            ShowMain();
            _mainWindow?.BeginManualUpdateCheck();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                if (_updateService is null)
                    return;

                var info = await _updateService.CheckForUpdatesAsync();
                if (!info.IsNewer)
                    return;

                Dispatcher.Invoke(() =>
                {
                    var dialog = new UpdateDialog(_updateService, info, false);
                    if (_mainWindow?.IsVisible == true)
                        dialog.Owner = _mainWindow;
                    else if (_miniWindow?.IsVisible == true)
                        dialog.Owner = _miniWindow;
                    dialog.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Startup update check failed: {ex.Message}");
            }
        }
    }
}
