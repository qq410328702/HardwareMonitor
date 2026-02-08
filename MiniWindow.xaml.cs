using HardwareMonitor.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace HardwareMonitor
{
    public partial class MiniWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly DispatcherTimer _barTimer;

        public event EventHandler? ExpandRequested;

        // Win32 drag support
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private static readonly IntPtr HT_CAPTION = new(0x2);

        public MiniWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;

            _barTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _barTimer.Tick += (_, _) => UpdateBars();
            _barTimer.Start();
            Loaded += (_, _) => UpdateBars();
        }

        private void UpdateBars()
        {
            SetBarWidth(CpuBar, _vm.CpuUsage / 100f);
            SetBarWidth(GpuBar, _vm.GpuUsage / 100f);
            SetBarWidth(MemBar, _vm.MemUsage / 100f);
        }

        private static void SetBarWidth(FrameworkElement bar, float ratio)
        {
            if (bar.Parent is FrameworkElement parent && parent.ActualWidth > 0)
                bar.Width = parent.ActualWidth * Math.Clamp(ratio, 0, 1);
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
            _barTimer.Stop();
            Close();
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            ExpandRequested?.Invoke(this, EventArgs.Empty);
            _barTimer.Stop();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _barTimer.Stop();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _barTimer.Stop();
            base.OnClosing(e);
        }
    }
}
