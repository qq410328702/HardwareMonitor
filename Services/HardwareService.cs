using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HardwareMonitor.Services;

public partial class HardwareService : IDisposable
{
    private Computer? _computer;
    private readonly object _syncLock = new();
    private readonly ILogger _logger;
    private volatile bool _ready;
    private string? _initError;

    private IHardware? _cpu;
    private IHardware? _gpu;
    private IHardware? _memory;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;

    public bool IsReady => _ready;
    public virtual string? InitError => _initError;

    public HardwareService(ILogger logger)
    {
        _logger = logger;
    }

    public virtual async Task InitAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var c = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsPsuEnabled = true,
                    IsStorageEnabled = true
                };
                c.Open();

                lock (_syncLock)
                {
                    _computer = c;
                    foreach (var hw in c.Hardware)
                    {
                        switch (hw.HardwareType)
                        {
                            case HardwareType.Cpu: _cpu = hw; break;
                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuAmd:
                            case HardwareType.GpuIntel: _gpu = hw; break;
                            case HardwareType.Memory: _memory = hw; break;
                        }
                    }
                }
                _logger.Info("硬件监控初始化成功");
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
                _logger.Error("Computer 初始化失败", ex);
            }

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _cpuCounter = null;
                _logger.Warn($"CPU PerformanceCounter 初始化失败: {ex.Message}");
            }

            try
            {
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _ramCounter.NextValue();
            }
            catch (Exception ex)
            {
                _ramCounter = null;
                _logger.Warn($"RAM PerformanceCounter 初始化失败: {ex.Message}");
            }

            _ready = true;
        });
    }

    public Computer? GetComputer()
    {
        lock (_syncLock)
        {
            return _computer;
        }
    }

    public virtual HardwareSnapshot GetSnapshot()
    {
        var snap = new HardwareSnapshot();
        if (!_ready) return snap;

        lock (_syncLock)
        {
            if (_cpu is not null)
            {
                try { _cpu.Update(); ReadCpu(_cpu, snap); }
                catch (Exception ex) { _logger.Warn($"CPU 读取失败: {ex.Message}"); }
            }
            if (_gpu is not null)
            {
                try { _gpu.Update(); ReadGpu(_gpu, snap); }
                catch (Exception ex) { _logger.Warn($"GPU 读取失败: {ex.Message}"); }
            }
            if (_memory is not null)
            {
                try { _memory.Update(); ReadMemory(_memory, snap); }
                catch (Exception ex) { _logger.Warn($"内存读取失败: {ex.Message}"); }
            }

            if (_computer is not null)
            {
                if (snap.CpuTemp <= 0)
                    try { ReadCpuTemperatureFallback(_computer, snap); }
                    catch (Exception ex) { _logger.Warn($"CPU 温度兜底读取失败: {ex.Message}"); }

                try { ReadPowerSummary(_computer, _cpu, _gpu, _memory, snap); }
                catch (Exception ex) { _logger.Warn($"功耗读取失败: {ex.Message}"); }
            }
        }

        if (snap.CpuUsage == 0 && _cpuCounter != null)
            try { snap.CpuUsage = _cpuCounter.NextValue(); }
            catch (Exception ex) { _logger.Warn($"CPU PerformanceCounter 读取失败: {ex.Message}"); }

        if (snap.MemUsage == 0 && _ramCounter != null)
        {
            try
            {
                float availMb = _ramCounter.NextValue();
                var gcInfo = GC.GetGCMemoryInfo();
                float totalMb = gcInfo.TotalAvailableMemoryBytes / 1024f / 1024f;
                if (totalMb > 0)
                {
                    snap.MemAvailable = availMb / 1024f;
                    snap.MemUsed = (totalMb - availMb) / 1024f;
                    snap.MemUsage = (totalMb - availMb) / totalMb * 100f;
                }
            }
            catch (Exception ex) { _logger.Warn($"RAM PerformanceCounter 读取失败: {ex.Message}"); }
        }

        return snap;
    }

    public virtual void Dispose()
    {
        lock (_syncLock)
        {
            _computer?.Close();
            _computer = null;
            _cpu = null;
            _gpu = null;
            _memory = null;
        }
        _cpuCounter?.Dispose();
        _cpuCounter = null;
        _ramCounter?.Dispose();
        _ramCounter = null;
    }
}
