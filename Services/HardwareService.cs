using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HardwareMonitor.Services;

public class HardwareService : IDisposable
{
    private Computer? _computer;
    private readonly object _syncLock = new();
    private readonly ILogger _logger;
    private volatile bool _ready;
    private string? _initError;

    // 缓存的硬件引用
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

    /// <summary>
    /// Heavy init on background thread — call once at startup.
    /// </summary>
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
                    IsMemoryEnabled = true
                };
                c.Open();

                lock (_syncLock)
                {
                    _computer = c;
                    // 缓存硬件引用
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
        }

        // PerformanceCounter fallback (outside lock since they're independent)
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


    private static void ReadCpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.CpuName = hw.Name;

        // Collect power sensors with priority: Package > Cores > any
        float powerPackage = 0, powerCores = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie"))
                    snap.CpuTemp = sensor.Value ?? 0;
                else if (snap.CpuTemp == 0)
                    snap.CpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                snap.CpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                if (v > 0)
                {
                    if (sensor.Name.Contains("Package"))
                        powerPackage = v;
                    else if (sensor.Name.Contains("Cores"))
                        powerCores = v;
                    else if (powerAny == 0)
                        powerAny = v;
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
                snap.CpuClock = sensor.Value ?? 0;
        }

        // Pick best power reading
        snap.CpuPower = powerPackage > 0 ? powerPackage
                       : powerCores > 0 ? powerCores
                       : powerAny;
    }

    private static void ReadGpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.GpuName = hw.Name;

        // Collect power sensors with priority:
        // "Board Power" / "GPU Power" / "TDP" (total board) > "Power" (generic) > any
        float powerBoard = 0, powerGpu = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"))
                    snap.GpuTemp = sensor.Value ?? 0;
                else if (snap.GpuTemp == 0)
                    snap.GpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                snap.GpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                string name = sensor.Name;
                if (v > 0)
                {
                    if (name.Contains("Board") || name.Contains("TDP") || name.Contains("Total"))
                        powerBoard = Math.Max(powerBoard, v);
                    else if (name.Contains("GPU") || name.Contains("Core"))
                        powerGpu = Math.Max(powerGpu, v);
                    else
                        powerAny = Math.Max(powerAny, v);
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                snap.GpuClock = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Used"))
                snap.GpuMemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Total"))
                snap.GpuMemTotal = sensor.Value ?? 0;
        }

        // Pick best power reading — board power is the true total
        snap.GpuPower = powerBoard > 0 ? powerBoard
                       : powerGpu > 0 ? powerGpu
                       : powerAny;
    }

    private static void ReadMemory(IHardware hw, HardwareSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                snap.MemUsage = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                snap.MemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                snap.MemAvailable = sensor.Value ?? 0;
        }
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

public class HardwareSnapshot
{
    public string CpuName { get; set; } = "CPU";
    public float CpuTemp { get; set; }
    public float CpuUsage { get; set; }
    public float CpuPower { get; set; }
    public float CpuClock { get; set; }
    public string GpuName { get; set; } = "GPU";
    public float GpuTemp { get; set; }
    public float GpuUsage { get; set; }
    public float GpuPower { get; set; }
    public float GpuClock { get; set; }
    public float GpuMemUsed { get; set; }
    public float GpuMemTotal { get; set; }
    public float MemUsage { get; set; }
    public float MemUsed { get; set; }
    public float MemAvailable { get; set; }
    public float MemTotal => MemUsed + MemAvailable;
}
