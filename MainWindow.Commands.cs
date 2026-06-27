using HardwareMonitor.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HardwareMonitor;

public partial class MainWindow
{
    private void ProcessSort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            if (Enum.TryParse<ProcessSortMode>(tag, out var mode))
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
        if (sender is Button btn && btn.Tag is AlertRule rule)
        {
            Vm.RemoveAlertRule(rule);
        }
    }
}
