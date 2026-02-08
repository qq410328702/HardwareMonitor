using HardwareMonitor.Services;
using HardwareMonitor.ViewModels;
using System.Windows;

namespace HardwareMonitor
{
    public partial class App : Application
    {
        private MainViewModel? _vm;
        private MainWindow? _mainWindow;

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Apply light theme as default
            ThemeService.Apply(1);

            // Create shared ViewModel
            _vm = new MainViewModel();

            // Create main window but keep it hidden
            _mainWindow = new MainWindow(_vm);

            // Show mini window directly
            ShowMini();
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
                    _vm?.Dispose();
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
