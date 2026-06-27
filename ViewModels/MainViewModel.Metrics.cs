using HardwareMonitor.Services;
using System.Collections.ObjectModel;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    private string _cpuName = "CPU";
    private float _cpuTemp, _cpuUsage, _cpuPower, _cpuClock;
    private string _gpuName = "GPU";
    private float _gpuTemp, _gpuUsage, _gpuPower, _gpuClock, _gpuMemUsed, _gpuMemTotal;
    private float _memUsage, _memUsed, _memTotal, _totalPower;
    private bool _hasPowerReadings;
    private string _powerSourceText = "已检测传感器汇总";
    private string _powerUnavailableReason = "";
    private bool _isLoading = true;
    private bool _isError;
    private string _errorMessage = "";
    private int _pollingIntervalMs = 1000;
    private int _themeIndex;

    public ObservableCollection<PowerReading> PowerReadings { get; } = new();

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
    public bool HasPowerReadings { get => _hasPowerReadings; set => SetField(ref _hasPowerReadings, value); }
    public string PowerSourceText { get => _powerSourceText; set => SetField(ref _powerSourceText, value); }
    public string PowerUnavailableReason { get => _powerUnavailableReason; set => SetField(ref _powerUnavailableReason, value); }
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

    private void ApplySnapshot(HardwareSnapshot s)
    {
        CpuName = s.CpuName; CpuTemp = s.CpuTemp; CpuUsage = s.CpuUsage;
        CpuPower = s.CpuPower; CpuClock = s.CpuClock;
        GpuName = s.GpuName; GpuTemp = s.GpuTemp; GpuUsage = s.GpuUsage;
        GpuPower = s.GpuPower; GpuClock = s.GpuClock;
        GpuMemUsed = s.GpuMemUsed; GpuMemTotal = s.GpuMemTotal;
        MemUsage = s.MemUsage; MemUsed = s.MemUsed; MemTotal = s.MemTotal;
        TotalPower = s.TotalPower;
        HasPowerReadings = s.HasPowerReadings;
        PowerSourceText = s.PowerSourceText;
        PowerUnavailableReason = s.PowerUnavailableReason;
        ReplaceCollection(PowerReadings, s.PowerReadings);
        UpdateElectricityCost(s);
    }
}
