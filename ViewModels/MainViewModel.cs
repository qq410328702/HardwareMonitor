using HardwareMonitor.Services;
using System;
using System.Threading;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly HardwareService _hw;
    private readonly IDiskMonitorService? _diskService;
    private readonly INetworkMonitorService? _networkService;
    private readonly IProcessMonitorService? _processService;
    private readonly IAlertEngine? _alertEngine;
    private readonly ITrayService? _trayService;
    private readonly IDataStorageService? _dataStorageService;
    private readonly CancellationTokenSource _cts = new();

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

        _ = StartAsync(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _hw.Dispose();
    }
}
