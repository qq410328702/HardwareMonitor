using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;

namespace HardwareMonitor.Services;

public sealed class HardwareService : IDisposable
{
    private Computer? _computer;
    private readonly bool _isAdmin;
    private volatile bool _ready;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;

    public bool IsAdmin => _isAdmin;
    public bool IsReady => _ready;

    public HardwareService()
    {
        _isAdmin = IsRunningAsAdmin();
    }

    /// <summary>
    /// Heavy init on background thread — call once at startup.
    /// </summary>
    public async Task InitAsync()
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
                _computer = c;
            }
            catch { _computer = null; }

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
            }
            catch { _cpuCounter = null; }

            try
            {
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _ramCounter.NextValue();
            }
            catch { _ramCounter = null; }

            _ready = true;
        });
    }

    public HardwareSnapshot GetSnapshot()
    {
        var snap = new HardwareSnapshot();
        if (!_ready) return snap;

        if (_computer != null)
        {
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    foreach (var sub in hw.SubHardware)
                        sub.Update();

                    switch (hw.HardwareType)
                    {
                        case HardwareType.Cpu: ReadCpu(hw, snap); break;
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuIntel: ReadGpu(hw, snap); break;
                        case HardwareType.Memory: ReadMemory(hw, snap); break;
                    }
                }
            }
            catch { }
        }

        if (snap.CpuUsage == 0 && _cpuCounter != null)
            try { snap.CpuUsage = _cpuCounter.NextValue(); } catch { }

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
            catch { }
        }

        return snap;
    }

    private static void ReadCpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.CpuName = hw.Name;
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                snap.CpuTemp = sensor.Value ?? 0;
            else if (sensor.SensorType == SensorType.Temperature && snap.CpuTemp == 0)
                snap.CpuTemp = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                snap.CpuUsage = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
                snap.CpuPower = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
                snap.CpuClock = sensor.Value ?? 0;
        }
    }

    private static void ReadGpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.GpuName = hw.Name;
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                snap.GpuTemp = sensor.Value ?? 0;
            else if (sensor.SensorType == SensorType.Temperature && snap.GpuTemp == 0)
                snap.GpuTemp = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                snap.GpuUsage = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Power)
                snap.GpuPower = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                snap.GpuClock = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Used"))
                snap.GpuMemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Total"))
                snap.GpuMemTotal = sensor.Value ?? 0;
        }
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

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public void Dispose()
    {
        _computer?.Close();
        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();
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
