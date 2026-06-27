using System;
using System.Globalization;
using System.Linq;

namespace HardwareMonitor.Services;

public enum DiskHealthStatus { Healthy, Warning, Critical }

public class DiskSnapshot
{
    public string LayoutCardId { get; set; } = "";
    public string Name { get; set; } = "";
    public float Temperature { get; set; }
    public float ReadSpeed { get; set; }
    public float WriteSpeed { get; set; }
    public DiskHealthStatus Health { get; set; }

    public float? LifeUsedPercent { get; set; }
    public float? LifeRemainingPercent { get; set; }
    public float? AvailableSparePercent { get; set; }
    public float? AvailableSpareThresholdPercent { get; set; }
    public long? CriticalWarning { get; set; }
    public double? TotalReadTb { get; set; }
    public double? TotalWrittenTb { get; set; }
    public long? PowerOnHours { get; set; }
    public long? PowerCycleCount { get; set; }
    public long? UnsafeShutdownCount { get; set; }
    public long? MediaErrorCount { get; set; }
    public long? ErrorLogEntryCount { get; set; }
    public long? WarningTemperatureMinutes { get; set; }
    public long? CriticalTemperatureMinutes { get; set; }
    public string DriveLetters { get; set; } = "";
    public string BusType { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string LifetimeStatusText { get; set; } = "";
    public string LifetimeUnavailableReason { get; set; } = "";

    public bool HasLifetimeMetrics =>
        LifeUsedPercent.HasValue ||
        LifeRemainingPercent.HasValue ||
        AvailableSparePercent.HasValue ||
        CriticalWarning.HasValue ||
        TotalReadTb.HasValue ||
        TotalWrittenTb.HasValue ||
        PowerOnHours.HasValue ||
        PowerCycleCount.HasValue ||
        UnsafeShutdownCount.HasValue ||
        MediaErrorCount.HasValue ||
        ErrorLogEntryCount.HasValue ||
        WarningTemperatureMinutes.HasValue ||
        CriticalTemperatureMinutes.HasValue;

    public bool HasLifePercent => LifeUsedPercent.HasValue || LifeRemainingPercent.HasValue;
    public bool HasLifetimeUnavailableReason => !string.IsNullOrWhiteSpace(LifetimeUnavailableReason);

    public float LifeRemainingBarPercent =>
        Math.Clamp(LifeRemainingPercent ?? (LifeUsedPercent.HasValue ? 100f - LifeUsedPercent.Value : 0f), 0f, 100f);

    public string DiskMetaText => string.Join(" / ", new[] { DriveLetters, BusType, MediaType }
        .Where(v => !string.IsNullOrWhiteSpace(v)));

    public string LifeRemainingDisplay => FormatPercent(LifeRemainingPercent);
    public string LifeUsedDisplay => FormatPercent(LifeUsedPercent);
    public string AvailableSpareDisplay => FormatPercent(AvailableSparePercent);
    public string TotalReadDisplay => FormatTb(TotalReadTb);
    public string TotalWrittenDisplay => FormatTb(TotalWrittenTb);
    public string PowerOnDisplay => PowerOnHours.HasValue ? $"{PowerOnHours.Value:N0} h" : "--";
    public string PowerCycleDisplay => FormatCount(PowerCycleCount);
    public string UnsafeShutdownDisplay => FormatCount(UnsafeShutdownCount);
    public string MediaErrorDisplay => FormatCount(MediaErrorCount);
    public string ErrorLogEntryDisplay => FormatCount(ErrorLogEntryCount);
    public string CriticalWarningDisplay => CriticalWarning.HasValue ? $"0x{CriticalWarning.Value:X2}" : "--";
    public string WarningTemperatureTimeDisplay => FormatMinutes(WarningTemperatureMinutes);
    public string CriticalTemperatureTimeDisplay => FormatMinutes(CriticalTemperatureMinutes);
    public string LayoutDisplayName => string.IsNullOrWhiteSpace(Name) ? "磁盘" : $"磁盘 - {Name}";

    private static string FormatPercent(float? value) => value.HasValue ? $"{value.Value:F0}%" : "--";
    private static string FormatTb(double? value) => value.HasValue ? $"{value.Value:F1} TB" : "--";
    private static string FormatCount(long? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.CurrentCulture) : "--";

    private static string FormatMinutes(long? minutes)
    {
        if (!minutes.HasValue)
            return "--";

        if (minutes.Value < 60)
            return $"{minutes.Value:N0} 分钟";

        double hours = minutes.Value / 60d;
        return $"{hours:N1} h";
    }
}
