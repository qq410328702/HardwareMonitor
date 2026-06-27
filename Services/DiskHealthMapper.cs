using System;

namespace HardwareMonitor.Services;

internal static class DiskHealthMapper
{
    private const string LifetimeUnavailableReason = "寿命信息需管理员权限或设备不支持 SMART";

    public static void FinalizeLifetimeStatus(DiskSnapshot snapshot)
    {
        if (!snapshot.LifeRemainingPercent.HasValue && snapshot.LifeUsedPercent.HasValue)
            snapshot.LifeRemainingPercent = Math.Clamp(100f - snapshot.LifeUsedPercent.Value, 0f, 100f);
        if (!snapshot.LifeUsedPercent.HasValue && snapshot.LifeRemainingPercent.HasValue)
            snapshot.LifeUsedPercent = Math.Clamp(100f - snapshot.LifeRemainingPercent.Value, 0f, 100f);

        snapshot.Health = MapSmartHealthStatus(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.LifetimeStatusText))
        {
            snapshot.LifetimeStatusText = snapshot.Health switch
            {
                DiskHealthStatus.Critical => "Critical",
                DiskHealthStatus.Warning => "Warning",
                _ => "Healthy / OK"
            };
        }

        if (!snapshot.HasLifetimeMetrics && string.IsNullOrWhiteSpace(snapshot.LifetimeUnavailableReason))
            snapshot.LifetimeUnavailableReason = LifetimeUnavailableReason;
    }

    public static DiskHealthStatus MapTemperature(float temperature)
    {
        if (temperature > 60f)
            return DiskHealthStatus.Critical;
        if (temperature > 50f)
            return DiskHealthStatus.Warning;
        return DiskHealthStatus.Healthy;
    }

    private static DiskHealthStatus MapSmartHealthStatus(DiskSnapshot snapshot)
    {
        if (snapshot.CriticalWarning.GetValueOrDefault() != 0 ||
            snapshot.MediaErrorCount.GetValueOrDefault() > 0 ||
            snapshot.Temperature > 60f ||
            IsUnhealthy(snapshot.LifetimeStatusText))
        {
            return DiskHealthStatus.Critical;
        }

        bool spareBelowThreshold = snapshot.AvailableSparePercent.HasValue &&
            snapshot.AvailableSpareThresholdPercent.HasValue &&
            snapshot.AvailableSparePercent.Value <= snapshot.AvailableSpareThresholdPercent.Value;

        if (snapshot.LifeUsedPercent.GetValueOrDefault() >= 80f ||
            snapshot.LifeRemainingPercent.GetValueOrDefault(100f) <= 20f ||
            spareBelowThreshold ||
            snapshot.Temperature > 50f ||
            IsWarning(snapshot.LifetimeStatusText))
        {
            return DiskHealthStatus.Warning;
        }

        return DiskHealthStatus.Healthy;
    }

    private static bool IsUnhealthy(string status) =>
        Contains(status, "Unhealthy") ||
        Contains(status, "Critical") ||
        Contains(status, "Error") ||
        Contains(status, "Failure") ||
        Contains(status, "Degraded");

    private static bool IsWarning(string status) =>
        Contains(status, "Warning") ||
        Contains(status, "Pred Fail") ||
        Contains(status, "Stressed");

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
