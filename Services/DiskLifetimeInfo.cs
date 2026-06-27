namespace HardwareMonitor.Services;

internal sealed class DiskLifetimeInfo
{
    public int? PhysicalDiskIndex { get; set; }
    public string FriendlyName { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string DriveLetters { get; set; } = "";
    public string BusType { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string HealthStatusText { get; set; } = "";
    public string ReliabilityUnavailableReason { get; set; } = "";
    public float? Temperature { get; set; }
    public float? LifeUsedPercent { get; set; }
    public float? LifeRemainingPercent { get; set; }
    public float? AvailableSparePercent { get; set; }
    public long? PowerOnHours { get; set; }
    public long? PowerCycleCount { get; set; }
    public long? MediaErrorCount { get; set; }
    public long? ErrorLogEntryCount { get; set; }

    public string NormalizedName => DiskLifetimeReader.Normalize(FriendlyName);

    public void ApplyTo(DiskSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.DriveLetters))
            snapshot.DriveLetters = DriveLetters;
        if (string.IsNullOrWhiteSpace(snapshot.BusType))
            snapshot.BusType = BusType;
        if (string.IsNullOrWhiteSpace(snapshot.MediaType))
            snapshot.MediaType = MediaType;
        if (string.IsNullOrWhiteSpace(snapshot.LifetimeStatusText))
            snapshot.LifetimeStatusText = HealthStatusText;

        if (snapshot.Temperature <= 0 && Temperature.HasValue)
            snapshot.Temperature = Temperature.Value;
        snapshot.LifeUsedPercent ??= LifeUsedPercent;
        snapshot.LifeRemainingPercent ??= LifeRemainingPercent;
        snapshot.AvailableSparePercent ??= AvailableSparePercent;
        snapshot.PowerOnHours ??= PowerOnHours;
        snapshot.PowerCycleCount ??= PowerCycleCount;
        snapshot.MediaErrorCount ??= MediaErrorCount;
        snapshot.ErrorLogEntryCount ??= ErrorLogEntryCount;

        if (!string.IsNullOrWhiteSpace(ReliabilityUnavailableReason) &&
            !snapshot.HasLifetimeMetrics)
        {
            snapshot.LifetimeUnavailableReason = ReliabilityUnavailableReason;
        }
    }
}
