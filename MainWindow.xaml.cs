using HardwareMonitor.Services;
using HardwareMonitor.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
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

            ThemeCombo.ItemsSource = ThemeService.ThemeNames;
            ThemeCombo.SelectedIndex = (int)ThemeService.Current;
        }

        private void ThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedIndex >= 0)
                ThemeService.Apply(ThemeCombo.SelectedIndex);
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
            Vm.Dispose();
            Application.Current.Shutdown();
        }

        private void MiniMode_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowMini();
        }
    }
}
