using HardwareMonitor.Services;
using System;
using System.Threading;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly HardwareService _hw;
    private readonly IDiskMonitorService? _diskService;
    private readonly ElectricityCostService _electricityCost;
    private readonly CancellationTokenSource _cts = new();

    public MainViewModel(
        HardwareService hw,
        IDiskMonitorService? diskService = null,
        ElectricityCostService? electricityCost = null)
    {
        _hw = hw;
        _diskService = diskService;
        _electricityCost = electricityCost ?? new ElectricityCostService();
        _themeIndex = (int)ThemeService.Current;
        InitializeElectricityCost();

        _ = StartAsync(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _electricityCost.Dispose();
        _hw.Dispose();
    }
}
