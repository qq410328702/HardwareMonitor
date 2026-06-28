using System;
using System.Collections.Generic;

namespace HardwareMonitor.Services;

internal static class DiskHealthMapper
{
    private const string LifetimeUnavailableReason = "寿命信息需管理员权限或设备不支持 SMART";
    private const string BadSectorUnavailableReason = "未检测到坏道相关 SMART 字段，可能需要管理员权限或设备不支持";

    public static void FinalizeLifetimeStatus(DiskSnapshot snapshot)
    {
        if (!snapshot.LifeRemainingPercent.HasValue && snapshot.LifeUsedPercent.HasValue)
            snapshot.LifeRemainingPercent = Math.Clamp(100f - snapshot.LifeUsedPercent.Value, 0f, 100f);
        if (!snapshot.LifeUsedPercent.HasValue && snapshot.LifeRemainingPercent.HasValue)
            snapshot.LifeUsedPercent = Math.Clamp(100f - snapshot.LifeRemainingPercent.Value, 0f, 100f);

        FinalizeBadSectorStatus(snapshot);
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
            snapshot.BadSectorRisk == DiskBadSectorRiskStatus.Critical ||
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
            snapshot.BadSectorRisk == DiskBadSectorRiskStatus.Warning ||
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

    private static void FinalizeBadSectorStatus(DiskSnapshot snapshot)
    {
        snapshot.BadSectorLastCheckedAt = snapshot.HasBadSectorMetrics ? DateTime.Now : null;

        var criticalReasons = new List<string>();
        AddReason(criticalReasons, "待映射扇区", snapshot.CurrentPendingSectorCount);
        AddReason(criticalReasons, "离线不可纠正", snapshot.OfflineUncorrectableSectorCount);
        AddReason(criticalReasons, "不可纠正读取", snapshot.UncorrectableReadErrorCount);
        AddReason(criticalReasons, "不可纠正写入", snapshot.UncorrectableWriteErrorCount);
        AddReason(criticalReasons, "不可纠正错误", snapshot.UncorrectableErrorCount);
        AddReason(criticalReasons, "介质完整性错误", snapshot.MediaErrorCount);

        if (criticalReasons.Count > 0)
        {
            snapshot.BadSectorRisk = DiskBadSectorRiskStatus.Critical;
            snapshot.BadSectorStatusText = "严重风险";
            snapshot.BadSectorRiskReason = string.Join("，", criticalReasons);
            snapshot.BadSectorUnavailableReason = "";
            return;
        }

        var warningReasons = new List<string>();
        AddReason(warningReasons, "重映射扇区", snapshot.ReallocatedSectorCount);
        AddReason(warningReasons, "错误日志", snapshot.ErrorLogEntryCount);

        if (warningReasons.Count > 0)
        {
            snapshot.BadSectorRisk = DiskBadSectorRiskStatus.Warning;
            snapshot.BadSectorStatusText = "需关注";
            snapshot.BadSectorRiskReason = string.Join("，", warningReasons);
            snapshot.BadSectorUnavailableReason = "";
            return;
        }

        if (snapshot.HasBadSectorMetrics)
        {
            snapshot.BadSectorRisk = DiskBadSectorRiskStatus.Healthy;
            snapshot.BadSectorStatusText = "未发现";
            snapshot.BadSectorRiskReason = "坏道相关 SMART 计数为 0";
            snapshot.BadSectorUnavailableReason = "";
            return;
        }

        snapshot.BadSectorRisk = DiskBadSectorRiskStatus.Unknown;
        snapshot.BadSectorStatusText = "未知";
        snapshot.BadSectorRiskReason = "";
        snapshot.BadSectorUnavailableReason = BadSectorUnavailableReason;
    }

    private static void AddReason(List<string> reasons, string label, long? value)
    {
        long count = value.GetValueOrDefault();
        if (count > 0)
            reasons.Add($"{label} {count:N0}");
    }
}
