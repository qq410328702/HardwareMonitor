using HardwareMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    private string _historyStatus = "";
    private string _historyRange = "1h";

    public ObservableCollection<SnapshotRecord> HistoryRecords { get; } = new();
    public string HistoryStatus { get => _historyStatus; set => SetField(ref _historyStatus, value); }
    public string HistoryRange { get => _historyRange; set => SetField(ref _historyRange, value); }

    public async Task LoadHistoryAsync(string range)
    {
        if (_dataStorageService is null) return;

        HistoryRange = range;
        HistoryStatus = "正在查询...";

        try
        {
            var (from, to) = HistoryViewModel.GetTimeRange(range);
            var records = await _dataStorageService.QueryAsync(from, to);

            RunOnUI(() =>
            {
                HistoryRecords.Clear();
                foreach (var r in records)
                    HistoryRecords.Add(r);
                HistoryStatus = $"共 {records.Count} 条记录";
            });
        }
        catch (Exception ex)
        {
            RunOnUI(() => HistoryStatus = $"查询失败: {ex.Message}");
        }
    }

    public async Task ExportHistoryCsvAsync()
    {
        if (_dataStorageService is null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"HardwareMonitor_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        HistoryStatus = "正在导出...";

        try
        {
            var (from, to) = HistoryViewModel.GetTimeRange(_historyRange);
            await _dataStorageService.ExportCsvAsync(from, to, dialog.FileName);
            HistoryStatus = $"已导出到 {System.IO.Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            HistoryStatus = $"导出失败: {ex.Message}";
        }
    }
}
