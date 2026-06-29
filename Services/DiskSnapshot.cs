using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace HardwareMonitor.Services;

public enum DiskHealthStatus { Healthy, Warning, Critical }

public enum DiskBadSectorRiskStatus { Unknown, Healthy, Warning, Critical }

public class DiskSnapshot : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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
    public long? ReallocatedSectorCount { get; set; }
    public long? CurrentPendingSectorCount { get; set; }
    public long? OfflineUncorrectableSectorCount { get; set; }
    public long? UncorrectableReadErrorCount { get; set; }
    public long? UncorrectableWriteErrorCount { get; set; }
    public long? UncorrectableErrorCount { get; set; }
    public long? WarningTemperatureMinutes { get; set; }
    public long? CriticalTemperatureMinutes { get; set; }
    public string DriveLetters { get; set; } = "";
    public string BusType { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string LifetimeStatusText { get; set; } = "";
    public string LifetimeUnavailableReason { get; set; } = "";
    public DiskBadSectorRiskStatus BadSectorRisk { get; set; } = DiskBadSectorRiskStatus.Unknown;
    public string BadSectorStatusText { get; set; } = "未知";
    public string BadSectorRiskReason { get; set; } = "";
    public string BadSectorUnavailableReason { get; set; } = "";
    public DateTime? BadSectorLastCheckedAt { get; set; }

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
        ReallocatedSectorCount.HasValue ||
        CurrentPendingSectorCount.HasValue ||
        OfflineUncorrectableSectorCount.HasValue ||
        UncorrectableReadErrorCount.HasValue ||
        UncorrectableWriteErrorCount.HasValue ||
        UncorrectableErrorCount.HasValue ||
        WarningTemperatureMinutes.HasValue ||
        CriticalTemperatureMinutes.HasValue;

    public bool HasLifePercent => LifeUsedPercent.HasValue || LifeRemainingPercent.HasValue;
    public bool HasLifetimeUnavailableReason => !string.IsNullOrWhiteSpace(LifetimeUnavailableReason);
    public bool HasReliabilityCounters =>
        PowerOnHours.HasValue ||
        PowerCycleCount.HasValue ||
        UnsafeShutdownCount.HasValue ||
        MediaErrorCount.HasValue ||
        ErrorLogEntryCount.HasValue;
    public bool HasTotalIo => TotalReadTb.HasValue || TotalWrittenTb.HasValue;
    public bool HasPowerCounters => PowerOnHours.HasValue || PowerCycleCount.HasValue;
    public bool HasShutdownOrCriticalWarning => UnsafeShutdownCount.HasValue || CriticalWarning.HasValue;
    public bool HasErrorCounters => MediaErrorCount.HasValue || ErrorLogEntryCount.HasValue;
    public bool HasBadSectorMetrics =>
        ReallocatedSectorCount.HasValue ||
        CurrentPendingSectorCount.HasValue ||
        OfflineUncorrectableSectorCount.HasValue ||
        UncorrectableReadErrorCount.HasValue ||
        UncorrectableWriteErrorCount.HasValue ||
        UncorrectableErrorCount.HasValue ||
        MediaErrorCount.HasValue ||
        ErrorLogEntryCount.HasValue;
    public bool HasBadSectorUnavailableReason => !string.IsNullOrWhiteSpace(BadSectorUnavailableReason);
    public bool HasBadSectorRiskReason => !string.IsNullOrWhiteSpace(BadSectorRiskReason);
    public bool HasDriveLetters => !string.IsNullOrWhiteSpace(DriveLetters);

    public float LifeRemainingBarPercent =>
        Math.Clamp(LifeRemainingPercent ?? (LifeUsedPercent.HasValue ? 100f - LifeUsedPercent.Value : 0f), 0f, 100f);

    public string DiskMetaText => string.Join(" / ", new[] { DriveLetters, BusType, MediaType }
        .Where(v => !string.IsNullOrWhiteSpace(v)));
    public string StorageGroupKey => DiskTemperaturePolicy.GetStorageGroupKey(this);
    public string StorageGroupTitle => DiskTemperaturePolicy.GetStorageGroupTitle(this);
    public string TemperatureRuleText => DiskTemperaturePolicy.GetTemperatureRuleText(this);
    public DiskHealthStatus TemperatureHealth => DiskTemperaturePolicy.MapTemperature(this);

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
    public string ReallocatedSectorDisplay => FormatCount(ReallocatedSectorCount);
    public string CurrentPendingSectorDisplay => FormatCount(CurrentPendingSectorCount);
    public string OfflineUncorrectableSectorDisplay => FormatCount(OfflineUncorrectableSectorCount);
    public string UncorrectableReadErrorDisplay => FormatCount(UncorrectableReadErrorCount);
    public string UncorrectableWriteErrorDisplay => FormatCount(UncorrectableWriteErrorCount);
    public string UncorrectableErrorDisplay => FormatCount(UncorrectableErrorCount);
    public string CriticalWarningDisplay => CriticalWarning.HasValue ? $"0x{CriticalWarning.Value:X2}" : "--";
    public string WarningTemperatureTimeDisplay => FormatMinutes(WarningTemperatureMinutes);
    public string CriticalTemperatureTimeDisplay => FormatMinutes(CriticalTemperatureMinutes);
    public string LayoutDisplayName => string.IsNullOrWhiteSpace(Name) ? "磁盘" : $"磁盘 - {Name}";
    public string BadSectorLastCheckedDisplay => BadSectorLastCheckedAt.HasValue
        ? BadSectorLastCheckedAt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
        : "--";

    private static string FormatPercent(float? value) => value.HasValue ? $"{value.Value:F0}%" : "--";
    private static string FormatTb(double? value) => value.HasValue ? $"{value.Value:F1} TB" : "--";
    private static string FormatCount(long? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.CurrentCulture) : "--";

    internal void UpdateFrom(DiskSnapshot source)
    {
        string oldStorageGroupKey = StorageGroupKey;

        LayoutCardId = source.LayoutCardId;
        Name = source.Name;
        Temperature = source.Temperature;
        ReadSpeed = source.ReadSpeed;
        WriteSpeed = source.WriteSpeed;
        Health = source.Health;
        LifeUsedPercent = source.LifeUsedPercent;
        LifeRemainingPercent = source.LifeRemainingPercent;
        AvailableSparePercent = source.AvailableSparePercent;
        AvailableSpareThresholdPercent = source.AvailableSpareThresholdPercent;
        CriticalWarning = source.CriticalWarning;
        TotalReadTb = source.TotalReadTb;
        TotalWrittenTb = source.TotalWrittenTb;
        PowerOnHours = source.PowerOnHours;
        PowerCycleCount = source.PowerCycleCount;
        UnsafeShutdownCount = source.UnsafeShutdownCount;
        MediaErrorCount = source.MediaErrorCount;
        ErrorLogEntryCount = source.ErrorLogEntryCount;
        ReallocatedSectorCount = source.ReallocatedSectorCount;
        CurrentPendingSectorCount = source.CurrentPendingSectorCount;
        OfflineUncorrectableSectorCount = source.OfflineUncorrectableSectorCount;
        UncorrectableReadErrorCount = source.UncorrectableReadErrorCount;
        UncorrectableWriteErrorCount = source.UncorrectableWriteErrorCount;
        UncorrectableErrorCount = source.UncorrectableErrorCount;
        WarningTemperatureMinutes = source.WarningTemperatureMinutes;
        CriticalTemperatureMinutes = source.CriticalTemperatureMinutes;
        DriveLetters = source.DriveLetters;
        BusType = source.BusType;
        MediaType = source.MediaType;
        LifetimeStatusText = source.LifetimeStatusText;
        LifetimeUnavailableReason = source.LifetimeUnavailableReason;
        BadSectorRisk = source.BadSectorRisk;
        BadSectorStatusText = source.BadSectorStatusText;
        BadSectorRiskReason = source.BadSectorRiskReason;
        BadSectorUnavailableReason = source.BadSectorUnavailableReason;
        BadSectorLastCheckedAt = source.BadSectorLastCheckedAt;

        if (!string.Equals(oldStorageGroupKey, StorageGroupKey, StringComparison.OrdinalIgnoreCase))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StorageGroupKey)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StorageGroupTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TemperatureRuleText)));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

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
