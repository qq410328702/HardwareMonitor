using HardwareMonitor.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace HardwareMonitor
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private static readonly IntPtr HT_CAPTION = new(0x2);

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ensure the application shuts down (and OnExit fires) when MainWindow
            // is closed via Alt+F4 or any other OS-level close path.
            Application.Current.Shutdown();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SendMessage(hwnd, WM_NCLBUTTONDOWN, HT_CAPTION, IntPtr.Zero);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MiniMode_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowMini();
        }
    }
}
