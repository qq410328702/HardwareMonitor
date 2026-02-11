using HardwareMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace HardwareMonitor.ViewModels;

public class HistoryViewModel : BaseViewModel
{
    private readonly IDataStorageService _storageService;

    private string _selectedRange = "1h";
    private bool _isLoading;
    private string _statusMessage = "";

    public string SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (SetField(ref _selectedRange, value))
                _ = LoadHistoryAsync();
        }
    }

    public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public ObservableCollection<SnapshotRecord> Records { get; set; } = new();

    public HistoryViewModel(IDataStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task LoadHistoryAsync()
    {
        IsLoading = true;
        StatusMessage = "";

        try
        {
            var (from, to) = GetTimeRange(_selectedRange);
            var records = await _storageService.QueryAsync(from, to);

            RunOnUI(() =>
            {
                Records = new ObservableCollection<SnapshotRecord>(records);
                OnPropertyChanged(nameof(Records));
                StatusMessage = $"共 {records.Count} 条记录";
            });
        }
        catch (Exception ex)
        {
            RunOnUI(() => StatusMessage = $"查询失败: {ex.Message}");
        }
        finally
        {
            RunOnUI(() => IsLoading = false);
        }
    }

    public async Task ExportCsvAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"HardwareMonitor_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsLoading = true;
        StatusMessage = "正在导出...";

        try
        {
            var (from, to) = GetTimeRange(_selectedRange);
            await _storageService.ExportCsvAsync(from, to, dialog.FileName);
            StatusMessage = $"已导出到 {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal static (DateTime from, DateTime to) GetTimeRange(string range)
    {
        var to = DateTime.UtcNow;
        var from = range switch
        {
            "1h" => to.AddHours(-1),
            "24h" => to.AddHours(-24),
            "7d" => to.AddDays(-7),
            "30d" => to.AddDays(-30),
            _ => to.AddHours(-1)
        };
        return (from, to);
    }

    private static void RunOnUI(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.BeginInvoke(action);
        else
            action();
    }
}
