using HardwareMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HardwareMonitor
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;
        private LayoutViewModel? _layoutVm;
        private Point _dragStartPoint;
        private bool _isDragging;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private static readonly IntPtr HT_CAPTION = new(0x2);

        // Map CardId -> named Border element
        private Dictionary<string, Border>? _cardElements;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
        }

        public void SetLayoutViewModel(LayoutViewModel layoutVm)
        {
            _layoutVm = layoutVm;
            LayoutCardList.ItemsSource = _layoutVm.Cards;
            if (IsLoaded)
                ApplyLayout();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildCardElementMap();
            ApplyLayout();
        }

        private void BuildCardElementMap()
        {
            _cardElements = new Dictionary<string, Border>
            {
                ["cpu"] = Card_cpu,
                ["gpu"] = Card_gpu,
                ["memory"] = Card_memory,
                ["disk"] = Card_disk,
                ["network"] = Card_network,
                ["process"] = Card_process,
                ["charts"] = Card_charts,
                ["history"] = Card_history,
                ["alert"] = Card_alert,
                ["layout"] = Card_layout
            };
        }

        private void ApplyLayout()
        {
            if (_layoutVm is null || _cardElements is null) return;

            // Remove all cards from panel
            var children = new List<UIElement>();
            foreach (UIElement child in CardPanel.Children)
                children.Add(child);
            CardPanel.Children.Clear();

            // Re-add in layout order, applying visibility
            foreach (var card in _layoutVm.Cards)
            {
                if (_cardElements.TryGetValue(card.CardId, out var element))
                {
                    element.Visibility = card.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    CardPanel.Children.Add(element);
                }
            }

            // Add any cards not in layout config (safety fallback)
            foreach (var kvp in _cardElements)
            {
                if (!CardPanel.Children.Contains(kvp.Value))
                    CardPanel.Children.Add(kvp.Value);
            }
        }

        // --- Drag and Drop ---

        private void Card_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not Border border) return;

            var pos = e.GetPosition(this);
            if (!_isDragging)
            {
                _dragStartPoint = pos;
                _isDragging = true;
                return;
            }

            var diff = pos - _dragStartPoint;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = false;
                var cardId = border.Tag as string;
                if (cardId is null) return;

                var data = new DataObject("CardId", cardId);
                DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
            }
        }

        private void Card_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("CardId"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void Card_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("CardId"))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        private void Card_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("CardId")) return;
            if (sender is not Border targetBorder) return;

            var sourceCardId = (string)e.Data.GetData("CardId");
            var targetCardId = targetBorder.Tag as string;

            if (sourceCardId == null || targetCardId == null || sourceCardId == targetCardId) return;
            if (_layoutVm is null) return;

            // Find indices in the LayoutViewModel
            var fromIndex = -1;
            var toIndex = -1;
            for (int i = 0; i < _layoutVm.Cards.Count; i++)
            {
                if (_layoutVm.Cards[i].CardId == sourceCardId) fromIndex = i;
                if (_layoutVm.Cards[i].CardId == targetCardId) toIndex = i;
            }

            if (fromIndex >= 0 && toIndex >= 0)
            {
                _layoutVm.MoveCard(fromIndex, toIndex);
                ApplyLayout();
            }

            e.Handled = true;
        }

        // --- Window chrome ---

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
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
            Hide();
        }

        private void MiniMode_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowMini();
        }

        private void ProcessSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (Enum.TryParse<Services.ProcessSortMode>(tag, out var mode))
                    Vm.ProcessSortMode = mode;
            }
            else
            {
                Vm.ToggleProcessSortMode();
            }
        }

        private void HistoryRange_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string range)
            {
                _ = Vm.LoadHistoryAsync(range);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            _ = Vm.ExportHistoryCsvAsync();
        }

        private void AddAlertRule_Click(object sender, RoutedEventArgs e)
        {
            Vm.AddAlertRule();
        }

        private void RemoveAlertRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Services.AlertRule rule)
            {
                Vm.RemoveAlertRule(rule);
            }
        }

        private void LayoutCard_Toggled(object sender, RoutedEventArgs e)
        {
            if (_layoutVm is null) return;
            if (sender is not CheckBox cb) return;
            if (cb.DataContext is not CardItemViewModel card) return;

            _layoutVm.SetCardVisibility(card.CardId, card.IsVisible);
            ApplyLayout();
        }
    }
}
