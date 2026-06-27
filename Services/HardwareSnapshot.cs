using System.Collections.Generic;

namespace HardwareMonitor.Services;

public sealed class PowerReading
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public float Watts { get; set; }
    public float SharePercent { get; set; }
    public bool IsIncludedInTotal { get; set; } = true;

    public string WattsDisplay => $"{Watts:F1} W";
    public string ShareDisplay => SharePercent > 0 ? $"{SharePercent:F0}%" : "--";
    public string InclusionText => IsIncludedInTotal ? "计入" : "参考";
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
    public float TotalPower { get; set; }
    public string PowerSourceText { get; set; } = "已检测传感器汇总";
    public string PowerUnavailableReason { get; set; } = "";
    public List<PowerReading> PowerReadings { get; } = new();
    public bool HasPowerReadings => PowerReadings.Count > 0 && TotalPower > 0;
}
