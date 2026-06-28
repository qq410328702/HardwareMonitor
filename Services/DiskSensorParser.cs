using LibreHardwareMonitor.Hardware;
using System;

namespace HardwareMonitor.Services;

internal static class DiskSensorParser
{
    public static DiskSnapshot ReadStorage(IHardware hw)
    {
        var snapshot = new DiskSnapshot { Name = hw.Name };

        foreach (var sensor in hw.Sensors)
        {
            float value = sensor.Value ?? 0;
            string name = sensor.Name ?? "";

            if (sensor.SensorType == SensorType.Temperature)
            {
                if (value > 0 && (snapshot.Temperature == 0 || IsPreferredTemperatureSensor(name)))
                    snapshot.Temperature = value;
            }
            else if (sensor.SensorType == SensorType.Throughput)
            {
                if (Contains(name, "Read"))
                    snapshot.ReadSpeed = value / (1024f * 1024f);
                else if (Contains(name, "Write"))
                    snapshot.WriteSpeed = value / (1024f * 1024f);
            }

            ReadLifetimeSensor(snapshot, sensor, value);
        }

        return snapshot;
    }

    private static void ReadLifetimeSensor(DiskSnapshot snapshot, ISensor sensor, float value)
    {
        if (sensor.Value is null)
            return;

        string name = sensor.Name ?? "";

        if (Contains(name, "Percentage Used"))
        {
            snapshot.LifeUsedPercent ??= value;
        }
        else if (Contains(name, "Remaining Life") ||
                 Contains(name, "Endurance Remaining") ||
                 Contains(name, "Media Wear Out Indicator"))
        {
            snapshot.LifeRemainingPercent ??= ClampPercent(value);
        }
        else if (Contains(name, "Available Spare Threshold"))
        {
            snapshot.AvailableSpareThresholdPercent ??= ClampPercent(value);
        }
        else if (Contains(name, "Available Spare") ||
                 Contains(name, "Available Reserved Space"))
        {
            snapshot.AvailableSparePercent ??= ClampPercent(value);
        }
        else if (Contains(name, "Critical Warning"))
        {
            snapshot.CriticalWarning ??= ToLong(value);
        }
        else if (IsTotalReadSensor(name))
        {
            snapshot.TotalReadTb ??= ConvertDataSensorToTb(sensor, value);
        }
        else if (IsTotalWriteSensor(name))
        {
            snapshot.TotalWrittenTb ??= ConvertDataSensorToTb(sensor, value);
        }
        else if (Contains(name, "Power On Hours") || Contains(name, "Power-On Hours"))
        {
            snapshot.PowerOnHours ??= ToLong(value);
        }
        else if (Contains(name, "Power Cycles") || Contains(name, "Power Cycle Count"))
        {
            snapshot.PowerCycleCount ??= ToLong(value);
        }
        else if (Contains(name, "Unsafe Shutdown") || Contains(name, "Unexpected Power Loss"))
        {
            snapshot.UnsafeShutdownCount ??= ToLong(value);
        }
        else if (IsReallocatedSectorSensor(name))
        {
            snapshot.ReallocatedSectorCount ??= ToLong(value);
        }
        else if (IsCurrentPendingSectorSensor(name))
        {
            snapshot.CurrentPendingSectorCount ??= ToLong(value);
        }
        else if (IsOfflineUncorrectableSectorSensor(name))
        {
            snapshot.OfflineUncorrectableSectorCount ??= ToLong(value);
        }
        else if (IsUncorrectableErrorSensor(name))
        {
            snapshot.UncorrectableErrorCount ??= ToLong(value);
        }
        else if (Contains(name, "Media Errors") || Contains(name, "Media and Data Integrity"))
        {
            snapshot.MediaErrorCount ??= ToLong(value);
        }
        else if (Contains(name, "Error Information Log Entries") || Contains(name, "Error Log Entries"))
        {
            snapshot.ErrorLogEntryCount ??= ToLong(value);
        }
        else if (Contains(name, "Warning Composite Temperature Time"))
        {
            snapshot.WarningTemperatureMinutes ??= ToLong(value);
        }
        else if (Contains(name, "Critical Composite Temperature Time"))
        {
            snapshot.CriticalTemperatureMinutes ??= ToLong(value);
        }
    }

    private static bool IsPreferredTemperatureSensor(string name) =>
        Contains(name, "Composite") || string.Equals(name, "Temperature", StringComparison.OrdinalIgnoreCase);

    private static bool IsTotalReadSensor(string name) =>
        Contains(name, "Data Read") ||
        Contains(name, "Data Units Read") ||
        Contains(name, "Total Bytes Read") ||
        Contains(name, "Total LBAs Read") ||
        Contains(name, "Sectors Read");

    private static bool IsTotalWriteSensor(string name) =>
        Contains(name, "Data Written") ||
        Contains(name, "Data Units Written") ||
        Contains(name, "Total Bytes Written") ||
        Contains(name, "Total LBAs Written") ||
        Contains(name, "Sectors Written") ||
        Contains(name, "Host Writes");

    private static bool IsReallocatedSectorSensor(string name) =>
        Contains(name, "Reallocated") &&
        (Contains(name, "Sector") || Contains(name, "Event"));

    private static bool IsCurrentPendingSectorSensor(string name) =>
        Contains(name, "Current Pending Sector") ||
        Contains(name, "Pending Sector Count") ||
        Contains(name, "Pending Sectors");

    private static bool IsOfflineUncorrectableSectorSensor(string name) =>
        Contains(name, "Offline Uncorrectable") ||
        Contains(name, "Uncorrectable Sector") ||
        Contains(name, "Uncorrectable Sectors");

    private static bool IsUncorrectableErrorSensor(string name) =>
        Contains(name, "Reported Uncorrectable") ||
        Contains(name, "Uncorrectable Error") ||
        Contains(name, "Uncorrectable Errors");

    private static double ConvertDataSensorToTb(ISensor sensor, float value)
    {
        string name = sensor.Name ?? "";
        const double bytesPerTiB = 1024d * 1024d * 1024d * 1024d;

        if (sensor.SensorType is SensorType.Data or SensorType.SmallData)
            return value / 1024d;
        if (Contains(name, "Data Units"))
            return value * 1000d * 512d / bytesPerTiB;
        if (Contains(name, "LBA") || Contains(name, "Sector"))
            return value * 512d / bytesPerTiB;
        if (Contains(name, "Bytes"))
            return value / bytesPerTiB;

        return value / 1024d;
    }

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static float ClampPercent(float value) => Math.Clamp(value, 0f, 100f);
    private static long ToLong(float value) => Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero));
}
