namespace HardwareMonitor.Services;

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
