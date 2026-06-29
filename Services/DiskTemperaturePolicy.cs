using System;

namespace HardwareMonitor.Services;

internal static class DiskTemperaturePolicy
{
    public const string NvmeGroupKey = "nvme";
    public const string SataSsdGroupKey = "sata-ssd";
    public const string HddGroupKey = "hdd";
    public const string OtherGroupKey = "other";

    public static string GetStorageGroupKey(DiskSnapshot snapshot)
    {
        string busType = snapshot.BusType ?? "";
        string mediaType = snapshot.MediaType ?? "";

        if (Contains(busType, "NVMe"))
            return NvmeGroupKey;

        if (Contains(mediaType, "HDD"))
            return HddGroupKey;

        if (Contains(busType, "SATA") && Contains(mediaType, "SSD"))
            return SataSsdGroupKey;

        return OtherGroupKey;
    }

    public static string GetStorageGroupTitle(DiskSnapshot snapshot) =>
        GetStorageGroupKey(snapshot) switch
        {
            NvmeGroupKey => "NVMe",
            SataSsdGroupKey => "SATA SSD",
            HddGroupKey => "HDD",
            _ => "其它设备"
        };

    public static string GetTemperatureRuleText(DiskSnapshot snapshot)
    {
        var rule = GetRule(snapshot);
        return rule.GroupKey == OtherGroupKey
            ? $"其它设备按 SATA SSD 规则：预警 {rule.WarningTemperature:F0}°C / 严重 {rule.CriticalTemperature:F0}°C"
            : $"{rule.Title} 预警 {rule.WarningTemperature:F0}°C / 严重 {rule.CriticalTemperature:F0}°C";
    }

    public static DiskHealthStatus MapTemperature(DiskSnapshot snapshot) =>
        MapTemperature(snapshot.Temperature, GetStorageGroupKey(snapshot));

    public static DiskHealthStatus MapTemperature(float temperature, string groupKey = OtherGroupKey)
    {
        var rule = GetRule(groupKey);

        if (temperature >= rule.CriticalTemperature)
            return DiskHealthStatus.Critical;
        if (temperature >= rule.WarningTemperature)
            return DiskHealthStatus.Warning;
        return DiskHealthStatus.Healthy;
    }

    private static TemperatureRule GetRule(DiskSnapshot snapshot) => GetRule(GetStorageGroupKey(snapshot));

    private static TemperatureRule GetRule(string groupKey) => groupKey switch
    {
        NvmeGroupKey => new TemperatureRule(NvmeGroupKey, "NVMe", 60f, 70f),
        HddGroupKey => new TemperatureRule(HddGroupKey, "HDD", 42f, 52f),
        SataSsdGroupKey => new TemperatureRule(SataSsdGroupKey, "SATA SSD", 50f, 60f),
        _ => new TemperatureRule(OtherGroupKey, "其它设备", 50f, 60f)
    };

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private sealed record TemperatureRule(
        string GroupKey,
        string Title,
        float WarningTemperature,
        float CriticalTemperature);
}
