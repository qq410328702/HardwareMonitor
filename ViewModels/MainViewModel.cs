using HardwareMonitor.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HardwareMonitor.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly HardwareService _hw;
    private readonly IDiskMonitorService? _diskService;
    private readonly INetworkMonitorService? _networkService;
    private readonly IProcessMonitorService? _processService;
    private readonly IAlertEngine? _alertEngine;
    private readonly ITrayService? _trayService;
    private readonly IDataStorageService? _dataStorageService;
    private readonly CancellationTokenSource _cts = new();
    private const int MaxPoints = 60;

    private string _cpuName = "CPU";
    private float _cpuTemp, _cpuUsage, _cpuPower, _cpuClock;
    private string _gpuName = "GPU";
    private float _gpuTemp, _gpuUsage, _gpuPower, _gpuClock, _gpuMemUsed, _gpuMemTotal;
    private float _memUsage, _memUsed, _memTotal, _totalPower;
    private bool _isLoading = true;
    private bool _isError;
    private string _errorMessage = "";
    private int _pollingIntervalMs = 1000;
    private int _themeIndex;
    private bool _isCpuTempAlert;
    private bool _isGpuTempAlert;
    private bool _isCpuUsageAlert;
    private bool _isGpuUsageAlert;

    private readonly ObservableCollection<ObservableValue> _cpuTempValues = new();
    private readonly ObservableCollection<ObservableValue> _gpuTempValues = new();
    private readonly ObservableCollection<ObservableValue> _cpuUsageValues = new();
    private readonly ObservableCollection<ObservableValue> _gpuUsageValues = new();
    private readonly ObservableCollection<ObservableValue> _memUsageValues = new();

    public string CpuName { get => _cpuName; set => SetField(ref _cpuName, value); }
    public float CpuTemp { get => _cpuTemp; set => SetField(ref _cpuTemp, value); }
    public float CpuUsage { get => _cpuUsage; set => SetField(ref _cpuUsage, value); }
    public float CpuPower { get => _cpuPower; set => SetField(ref _cpuPower, value); }
    public float CpuClock { get => _cpuClock; set => SetField(ref _cpuClock, value); }
    public string GpuName { get => _gpuName; set => SetField(ref _gpuName, value); }
    public float GpuTemp { get => _gpuTemp; set => SetField(ref _gpuTemp, value); }
    public float GpuUsage { get => _gpuUsage; set => SetField(ref _gpuUsage, value); }
    public float GpuPower { get => _gpuPower; set => SetField(ref _gpuPower, value); }
    public float GpuClock { get => _gpuClock; set => SetField(ref _gpuClock, value); }
    public float GpuMemUsed { get => _gpuMemUsed; set => SetField(ref _gpuMemUsed, value); }
    public float GpuMemTotal { get => _gpuMemTotal; set => SetField(ref _gpuMemTotal, value); }
    public float MemUsage { get => _memUsage; set => SetField(ref _memUsage, value); }
    public float MemUsed { get => _memUsed; set => SetField(ref _memUsed, value); }
    public float MemTotal { get => _memTotal; set => SetField(ref _memTotal, value); }
    public float TotalPower { get => _totalPower; set => SetField(ref _totalPower, value); }
    public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
    public bool IsError { get => _isError; set => SetField(ref _isError, value); }
    public string ErrorMessage { get => _errorMessage; set => SetField(ref _errorMessage, value); }
    public int PollingIntervalMs { get => _pollingIntervalMs; set => SetField(ref _pollingIntervalMs, value); }
    public int ThemeIndex
    {
        get => _themeIndex;
        set { if (SetField(ref _themeIndex, value)) ThemeService.Apply(value); }
    }
    public string[] ThemeNames => ThemeService.ThemeNames;

    public ObservableCollection<DiskSnapshot> DiskSnapshots { get; } = new();
    public ObservableCollection<NetworkSnapshot> NetworkSnapshots { get; } = new();
    public ObservableCollection<ProcessInfo> TopProcesses { get; } = new();
    public ObservableCollection<SnapshotRecord> HistoryRecords { get; } = new();

    private string _historyStatus = "";
    private string _historyRange = "1h";

    public string HistoryStatus { get => _historyStatus; set => SetField(ref _historyStatus, value); }
    public string HistoryRange { get => _historyRange; set => SetField(ref _historyRange, value); }

    private ProcessSortMode _processSortMode = ProcessSortMode.ByCpu;
    public ProcessSortMode ProcessSortMode
    {
        get => _processSortMode;
        set => SetField(ref _processSortMode, value);
    }

    public void ToggleProcessSortMode()
    {
        ProcessSortMode = ProcessSortMode == ProcessSortMode.ByCpu
            ? ProcessSortMode.ByMemory
            : ProcessSortMode.ByCpu;
    }

    // Alert rule configuration
    public ObservableCollection<AlertRule> AlertRules { get; } = new();

    private MetricType _newRuleMetric = MetricType.CpuTemp;
    public MetricType NewRuleMetric { get => _newRuleMetric; set => SetField(ref _newRuleMetric, value); }

    private string _newRuleThreshold = "80";
    public string NewRuleThreshold { get => _newRuleThreshold; set => SetField(ref _newRuleThreshold, value); }

    private CompareDirection _newRuleDirection = CompareDirection.Above;
    public CompareDirection NewRuleDirection { get => _newRuleDirection; set => SetField(ref _newRuleDirection, value); }

    private string _alertRuleStatus = "";
    public string AlertRuleStatus { get => _alertRuleStatus; set => SetField(ref _alertRuleStatus, value); }

    public MetricType[] MetricTypes => (MetricType[])Enum.GetValues(typeof(MetricType));
    public CompareDirection[] CompareDirections => (CompareDirection[])Enum.GetValues(typeof(CompareDirection));

    public void AddAlertRule()
    {
        if (_alertEngine is null)
        {
            AlertRuleStatus = "告警引擎未初始化";
            return;
        }

        if (!float.TryParse(_newRuleThreshold, out var threshold))
        {
            AlertRuleStatus = "阈值必须为有效数字";
            return;
        }

        var rule = new AlertRule
        {
            Metric = _newRuleMetric,
            Threshold = threshold,
            Direction = _newRuleDirection
        };

        var result = _alertEngine.AddRule(rule);
        if (result.IsSuccess)
        {
            AlertRules.Add(rule);
            AlertRuleStatus = "规则已添加";
        }
        else
        {
            AlertRuleStatus = result.ErrorMessage ?? "添加失败";
        }
    }

    public void RemoveAlertRule(AlertRule rule)
    {
        if (_alertEngine is null) return;
        _alertEngine.RemoveRule(rule);
        AlertRules.Remove(rule);
        AlertRuleStatus = "规则已删除";
    }

    public bool IsCpuTempAlert { get => _isCpuTempAlert; set => SetField(ref _isCpuTempAlert, value); }
    public bool IsGpuTempAlert { get => _isGpuTempAlert; set => SetField(ref _isGpuTempAlert, value); }
    public bool IsCpuUsageAlert { get => _isCpuUsageAlert; set => SetField(ref _isCpuUsageAlert, value); }
    public bool IsGpuUsageAlert { get => _isGpuUsageAlert; set => SetField(ref _isGpuUsageAlert, value); }

    public ISeries[] TempSeries { get; }
    public ISeries[] UsageSeries { get; }
    public Axis[] HiddenAxes { get; } = [new Axis { ShowSeparatorLines = false, IsVisible = false }];
    public Axis[] TempYAxes { get; } = [new Axis
    {
        MinLimit = 0, MaxLimit = 105, ShowSeparatorLines = false,
        LabelsPaint = new SolidColorPaint(new SKColor(0x8B, 0x94, 0x9E))
    }];
    public Axis[] UsageYAxes { get; } = [new Axis
    {
        MinLimit = 0, MaxLimit = 105, ShowSeparatorLines = false,
        LabelsPaint = new SolidColorPaint(new SKColor(0x8B, 0x94, 0x9E))
    }];

    public MainViewModel(
        HardwareService hw,
        IDiskMonitorService? diskService = null,
        INetworkMonitorService? networkService = null,
        IProcessMonitorService? processService = null,
        IAlertEngine? alertEngine = null,
        ITrayService? trayService = null,
        IDataStorageService? dataStorageService = null)
    {
        _hw = hw;
        _diskService = diskService;
        _networkService = networkService;
        _processService = processService;
        _alertEngine = alertEngine;
        _trayService = trayService;
        _dataStorageService = dataStorageService;
        _themeIndex = (int)ThemeService.Current;

        TempSeries =
        [
            MakeLine(_cpuTempValues, "CPU", 0x58, 0xA6, 0xFF),
            MakeLine(_gpuTempValues, "GPU", 0x3F, 0xB9, 0x50)
        ];
        UsageSeries =
        [
            MakeLine(_cpuUsageValues, "CPU", 0x58, 0xA6, 0xFF),
            MakeLine(_gpuUsageValues, "GPU", 0x3F, 0xB9, 0x50),
            MakeLine(_memUsageValues, "RAM", 0xBC, 0x8C, 0xFF)
        ];

        // Init hardware async, then start polling
        _ = StartAsync(_cts.Token);
    }

    private async Task StartAsync(CancellationToken ct)
    {
        try
        {
            // Heavy init on background thread — UI is already visible
            await _hw.InitAsync();

            // Check if InitAsync encountered an error internally
            if (_hw.InitError is not null)
            {
                RunOnUI(() =>
                {
                    IsLoading = false;
                    IsError = true;
                    ErrorMessage = $"硬件初始化失败: {_hw.InitError}";
                });
                return;
            }

            RunOnUI(() => IsLoading = false);

            // Start polling loop
            await PollAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected during shutdown — do not treat as error
        }
        catch (Exception ex)
        {
            RunOnUI(() =>
            {
                IsLoading = false;
                IsError = true;
                ErrorMessage = $"监控异常: {ex.Message}";
            });
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var s = await Task.Run(() => _hw.GetSnapshot(), ct);
                RunOnUI(() =>
                {
                    ApplySnapshot(s);
                    EvaluateAlerts(s);
                });

                // Persist snapshot to storage — errors must not interrupt polling
                if (_dataStorageService is not null)
                {
                    try
                    {
                        await _dataStorageService.SaveSnapshotAsync(s);
                    }
                    catch (Exception)
                    {
                        // 数据存储失败不中断轮询 (Requirement 2.7)
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // 单次轮询失败不终止循环，继续下一次
            }

            // Poll disk snapshots
            if (_diskService is not null)
            {
                try
                {
                    var disks = await Task.Run(() => _diskService.GetDiskSnapshots(), ct);
                    RunOnUI(() => ApplyDiskSnapshots(disks));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // 磁盘轮询失败不终止循环
                }
            }

            // Poll network snapshots
            if (_networkService is not null)
            {
                try
                {
                    var nets = await Task.Run(() => _networkService.GetNetworkSnapshots(), ct);
                    RunOnUI(() => ApplyNetworkSnapshots(nets));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // 网络轮询失败不终止循环
                }
            }

            // Poll top processes
            if (_processService is not null)
            {
                try
                {
                    var sortMode = _processSortMode;
                    var procs = await Task.Run(() => _processService.GetTopProcesses(10, sortMode), ct);
                    RunOnUI(() => ApplyTopProcesses(procs));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // 进程轮询失败不终止循环
                }
            }

            try
            {
                await Task.Delay(PollingIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private void ApplySnapshot(HardwareSnapshot s)
    {
        CpuName = s.CpuName; CpuTemp = s.CpuTemp; CpuUsage = s.CpuUsage;
        CpuPower = s.CpuPower; CpuClock = s.CpuClock;
        GpuName = s.GpuName; GpuTemp = s.GpuTemp; GpuUsage = s.GpuUsage;
        GpuPower = s.GpuPower; GpuClock = s.GpuClock;
        GpuMemUsed = s.GpuMemUsed; GpuMemTotal = s.GpuMemTotal;
        MemUsage = s.MemUsage; MemUsed = s.MemUsed; MemTotal = s.MemTotal;
        TotalPower = s.CpuPower + s.GpuPower;

        Push(_cpuTempValues, s.CpuTemp);
        Push(_gpuTempValues, s.GpuTemp);
        Push(_cpuUsageValues, s.CpuUsage);
        Push(_gpuUsageValues, s.GpuUsage);
        Push(_memUsageValues, s.MemUsage);
    }

    private static void Push(ObservableCollection<ObservableValue> col, float val)
    {
        col.Add(new ObservableValue(val));
        if (col.Count > MaxPoints) col.RemoveAt(0);
    }

    private void ApplyDiskSnapshots(List<DiskSnapshot> disks)
    {
        DiskSnapshots.Clear();
        foreach (var d in disks)
            DiskSnapshots.Add(d);
    }

    private void ApplyNetworkSnapshots(List<NetworkSnapshot> nets)
    {
        NetworkSnapshots.Clear();
        foreach (var n in nets)
            NetworkSnapshots.Add(n);
    }

    private void ApplyTopProcesses(List<ProcessInfo> procs)
    {
        TopProcesses.Clear();
        foreach (var p in procs)
            TopProcesses.Add(p);
    }

    private void EvaluateAlerts(HardwareSnapshot snapshot)
    {
        if (_alertEngine is null) return;

        try
        {
            var results = _alertEngine.Evaluate(snapshot);

            // Update alert state properties for UI binding
            IsCpuTempAlert = results.Any(r => r.Rule.Metric == MetricType.CpuTemp && r.IsTriggered)
                || (_isCpuTempAlert && results.Any(r => r.Rule.Metric == MetricType.CpuTemp && r.CurrentValue > r.Rule.Threshold));
            IsGpuTempAlert = results.Any(r => r.Rule.Metric == MetricType.GpuTemp && r.IsTriggered)
                || (_isGpuTempAlert && results.Any(r => r.Rule.Metric == MetricType.GpuTemp && r.CurrentValue > r.Rule.Threshold));
            IsCpuUsageAlert = results.Any(r => r.Rule.Metric == MetricType.CpuUsage && r.IsTriggered)
                || (_isCpuUsageAlert && results.Any(r => r.Rule.Metric == MetricType.CpuUsage && r.CurrentValue > r.Rule.Threshold));
            IsGpuUsageAlert = results.Any(r => r.Rule.Metric == MetricType.GpuUsage && r.IsTriggered)
                || (_isGpuUsageAlert && results.Any(r => r.Rule.Metric == MetricType.GpuUsage && r.CurrentValue > r.Rule.Threshold));

            // Clear alert state when value recovers
            foreach (var result in results)
            {
                bool exceeded = result.Rule.Direction == CompareDirection.Above
                    ? result.CurrentValue > result.Rule.Threshold
                    : result.CurrentValue < result.Rule.Threshold;

                if (!exceeded)
                {
                    switch (result.Rule.Metric)
                    {
                        case MetricType.CpuTemp: IsCpuTempAlert = false; break;
                        case MetricType.GpuTemp: IsGpuTempAlert = false; break;
                        case MetricType.CpuUsage: IsCpuUsageAlert = false; break;
                        case MetricType.GpuUsage: IsGpuUsageAlert = false; break;
                    }
                }
            }

            // Send toast notifications for newly triggered alerts
            foreach (var result in results.Where(r => r.IsTriggered))
            {
                var metricName = result.Rule.Metric switch
                {
                    MetricType.CpuTemp => "CPU 温度",
                    MetricType.GpuTemp => "GPU 温度",
                    MetricType.CpuUsage => "CPU 使用率",
                    MetricType.GpuUsage => "GPU 使用率",
                    _ => result.Rule.Metric.ToString()
                };

                var unit = result.Rule.Metric is MetricType.CpuTemp or MetricType.GpuTemp ? "°C" : "%";
                var direction = result.Rule.Direction == CompareDirection.Above ? "超过" : "低于";

                var title = $"⚠ {metricName}告警";
                var text = $"{metricName}当前值 {result.CurrentValue:F1}{unit} {direction}阈值 {result.Rule.Threshold:F1}{unit}";

                _trayService?.ShowBalloonTip(title, text);
            }
        }
        catch (Exception)
        {
            // Alert evaluation failure should not interrupt polling
        }
    }

    private static LineSeries<ObservableValue> MakeLine(
        ObservableCollection<ObservableValue> values, string name, byte r, byte g, byte b)
        => new()
        {
            Values = values, Name = name,
            Stroke = new SolidColorPaint(new SKColor(r, g, b)) { StrokeThickness = 2 },
            GeometrySize = 0, GeometryStroke = null, GeometryFill = null,
            Fill = new SolidColorPaint(new SKColor(r, g, b, 0x28)),
            LineSmoothness = 0.65
        };

    /// <summary>
    /// Dispatches an action to the UI thread if a WPF Dispatcher is available,
    /// otherwise executes it directly (e.g., in unit test environments).
    /// </summary>
    private static void RunOnUI(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.BeginInvoke(action);
        else
            action();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _hw.Dispose();
    }

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
