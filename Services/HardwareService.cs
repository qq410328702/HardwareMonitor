using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Security.Principal;

namespace HardwareMonitor.Services;

public sealed class HardwareService : IDisposable
{
    private readonly Computer? _computer;
    private readonly bool _isAdmin;

    // Fallback counters for non-admin
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _ramCounter;

    public bool IsAdmin => _isAdmin;

    public HardwareService()
    {
        _isAdmin = IsRunningAsAdmin();

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true
            };
            _computer.Open();
        }
        catch
        {
            _computer = null;
        }

        // Always init fallback counters
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // first call always returns 0
        }
        catch { _cpuCounter = null; }

        try
        {
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            _ramCounter.NextValue();
        }
        catch { _ramCounter = null; }
    }

    public HardwareSnapshot GetSnapshot()
    {
        var snap = new HardwareSnapshot();

        // Try LibreHardwareMonitor first
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
                        case HardwareType.Cpu:
                            ReadCpu(hw, snap);
                            break;
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuIntel:
                            ReadGpu(hw, snap);
                            break;
                        case HardwareType.Memory:
                            ReadMemory(hw, snap);
                            break;
                    }
                }
            }
            catch { /* sensor read failed, fall through to fallback */ }
        }

        // Fallback: if CPU usage is still 0, use PerformanceCounter
        if (snap.CpuUsage == 0 && _cpuCounter != null)
        {
            try { snap.CpuUsage = _cpuCounter.NextValue(); } catch { }
        }

        // Fallback: if memory not read, use PerformanceCounter + GC info
        if (snap.MemUsage == 0 && _ramCounter != null)
        {
            try
            {
                float availMb = _ramCounter.NextValue();
                var gcInfo = GC.GetGCMemoryInfo();
                float totalMb = gcInfo.TotalAvailableMemoryBytes / 1024f / 1024f;
                if (totalMb > 0)
                {
                    snap.MemAvailable = availMb / 1024f; // GB
                    snap.MemUsed = (totalMb - availMb) / 1024f; // GB
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
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
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
