using HardwareMonitor.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace HardwareMonitor
{
    public partial class MiniWindow : Window
    {
        public event EventHandler? ExpandRequested;

        // Win32 drag support
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private static readonly IntPtr HT_CAPTION = new(0x2);

        public MiniWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;
        }

        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Use Win32 to initiate window drag — works reliably everywhere
                var hwnd = new WindowInteropHelper(this).Handle;
                SendMessage(hwnd, WM_NCLBUTTONDOWN, HT_CAPTION, IntPtr.Zero);
            }
        }

        private void DragArea_MouseRightDown(object sender, MouseButtonEventArgs e)
        {
            // Minimize to tray instead of closing
            Hide();
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            ExpandRequested?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Minimize to tray instead of exiting
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Cancel close and hide to tray instead
            e.Cancel = true;
            Hide();
        }
    }
}
