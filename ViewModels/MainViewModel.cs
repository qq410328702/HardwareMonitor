using HardwareMonitor.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HardwareMonitor.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly HardwareService _hw;
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

    public MainViewModel(HardwareService hw)
    {
        _hw = hw;
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
                RunOnUI(() => ApplySnapshot(s));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // 单次轮询失败不终止循环，继续下一次
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
}
