using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace HardwareMonitor.Services;

internal sealed class DiskLifetimeReader
{
    private static readonly Regex PhysicalDriveRegex = new(
        @"(?:physicaldrive|/hdd/|/storage/)\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<DiskLifetimeInfo> Read()
    {
        var infos = ReadPhysicalDisks();
        var letters = ReadDriveLettersByDiskIndex();

        foreach (var info in infos.Values)
        {
            if (info.PhysicalDiskIndex.HasValue &&
                letters.TryGetValue(info.PhysicalDiskIndex.Value, out var driveLetters))
            {
                info.DriveLetters = string.Join(", ", driveLetters.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            }
        }

        ReadReliabilityCounters(infos);
        return infos.Values.ToList();
    }

    public static DiskLifetimeInfo? FindBestMatch(
        IHardware hardware,
        DiskSnapshot snapshot,
        IReadOnlyList<DiskLifetimeInfo> infos)
    {
        if (infos.Count == 0)
            return null;

        var physicalIndex = TryParsePhysicalDiskIndex(hardware);
        if (physicalIndex.HasValue)
        {
            var byIndex = infos.FirstOrDefault(i => i.PhysicalDiskIndex == physicalIndex.Value);
            if (byIndex is not null)
                return byIndex;
        }

        string hardwareName = Normalize(snapshot.Name);
        return infos
            .Where(i => !string.IsNullOrWhiteSpace(i.NormalizedName))
            .OrderByDescending(i => i.NormalizedName.Length)
            .FirstOrDefault(i => hardwareName.Contains(i.NormalizedName, StringComparison.OrdinalIgnoreCase) ||
                                 i.NormalizedName.Contains(hardwareName, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();
        return new string(chars);
    }

    public static string CreateLayoutCardId(IHardware hardware, string diskName, DiskLifetimeInfo? info)
    {
        int? physicalIndex = info?.PhysicalDiskIndex ?? TryParsePhysicalDiskIndex(hardware);
        if (physicalIndex.HasValue)
            return $"disk:{physicalIndex.Value}";

        string serial = Normalize(info?.SerialNumber ?? "");
        if (!string.IsNullOrWhiteSpace(serial))
            return $"disk:{serial.ToLowerInvariant()}";

        string name = Normalize(diskName);
        if (!string.IsNullOrWhiteSpace(name))
            return $"disk:{name.ToLowerInvariant()}";

        string identifier = Normalize(hardware.Identifier?.ToString() ?? "");
        return string.IsNullOrWhiteSpace(identifier)
            ? "disk:unknown"
            : $"disk:{identifier.ToLowerInvariant()}";
    }

    private static Dictionary<string, DiskLifetimeInfo> ReadPhysicalDisks()
    {
        var infos = new Dictionary<string, DiskLifetimeInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DeviceId,FriendlyName,SerialNumber,BusType,MediaType,HealthStatus,OperationalStatus FROM MSFT_PhysicalDisk");

            foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
            {
                string deviceId = ReadString(disk, "DeviceId");
                if (string.IsNullOrWhiteSpace(deviceId))
                    continue;

                var info = new DiskLifetimeInfo
                {
                    PhysicalDiskIndex = ReadInt(disk, "DeviceId"),
                    FriendlyName = ReadString(disk, "FriendlyName"),
                    SerialNumber = ReadString(disk, "SerialNumber"),
                    BusType = MapBusType(ReadUInt(disk, "BusType")),
                    MediaType = MapMediaType(ReadUInt(disk, "MediaType")),
                    HealthStatusText = BuildStatusText(
                        ReadUInt(disk, "HealthStatus"),
                        ReadUIntArray(disk, "OperationalStatus"))
                };

                infos[deviceId] = info;
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return infos;
    }

    private static void ReadReliabilityCounters(Dictionary<string, DiskLifetimeInfo> infos)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DeviceId,Temperature,Wear,PowerOnHours,StartStopCycleCount,ReadErrorsTotal,WriteErrorsTotal,ReadErrorsUncorrected,WriteErrorsUncorrected FROM MSFT_StorageReliabilityCounter");

            foreach (ManagementObject counter in searcher.Get().Cast<ManagementObject>())
            {
                string deviceId = ReadString(counter, "DeviceId");
                if (string.IsNullOrWhiteSpace(deviceId))
                    continue;

                if (!infos.TryGetValue(deviceId, out var info))
                {
                    info = new DiskLifetimeInfo { PhysicalDiskIndex = ReadInt(counter, "DeviceId") };
                    infos[deviceId] = info;
                }

                info.Temperature ??= ReadFloat(counter, "Temperature");
                info.LifeUsedPercent ??= ReadFloat(counter, "Wear");
                if (info.LifeUsedPercent.HasValue)
                    info.LifeRemainingPercent ??= Math.Clamp(100f - info.LifeUsedPercent.Value, 0f, 100f);
                info.PowerOnHours ??= ReadLong(counter, "PowerOnHours");
                info.PowerCycleCount ??= ReadLong(counter, "StartStopCycleCount");

                long? uncorrectedRead = ReadLong(counter, "ReadErrorsUncorrected");
                long? uncorrectedWrite = ReadLong(counter, "WriteErrorsUncorrected");
                long? readErrors = ReadLong(counter, "ReadErrorsTotal");
                long? writeErrors = ReadLong(counter, "WriteErrorsTotal");

                info.MediaErrorCount ??= SumNonNull(uncorrectedRead, uncorrectedWrite);
                info.ErrorLogEntryCount ??= SumNonNull(readErrors, writeErrors);
            }
        }
        catch (ManagementException)
        {
            MarkReliabilityUnavailable(infos);
        }
        catch (UnauthorizedAccessException)
        {
            MarkReliabilityUnavailable(infos);
        }
    }

    private static Dictionary<int, List<string>> ReadDriveLettersByDiskIndex()
    {
        var result = new Dictionary<int, List<string>>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT DeviceID,Index FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
            {
                int? index = ReadInt(disk, "Index");
                if (!index.HasValue)
                    continue;

                foreach (ManagementObject partition in disk.GetRelated("Win32_DiskPartition").Cast<ManagementObject>())
                {
                    foreach (ManagementObject logicalDisk in partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>())
                    {
                        string letter = ReadString(logicalDisk, "Name");
                        if (string.IsNullOrWhiteSpace(letter))
                            continue;

                        if (!result.TryGetValue(index.Value, out var letters))
                        {
                            letters = new List<string>();
                            result[index.Value] = letters;
                        }

                        if (!letters.Contains(letter, StringComparer.OrdinalIgnoreCase))
                            letters.Add(letter);
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return result;
    }

    private static void MarkReliabilityUnavailable(Dictionary<string, DiskLifetimeInfo> infos)
    {
        const string reason = "通电/错误计数需管理员权限或设备不支持 SMART";

        if (infos.Count == 0)
        {
            infos["unknown"] = new DiskLifetimeInfo { ReliabilityUnavailableReason = reason };
            return;
        }

        foreach (var info in infos.Values)
            info.ReliabilityUnavailableReason = reason;
    }

    private static int? TryParsePhysicalDiskIndex(IHardware hardware)
    {
        foreach (string candidate in new[] { hardware.Identifier?.ToString() ?? "", hardware.Name ?? "" })
        {
            var match = PhysicalDriveRegex.Match(candidate);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                return index;
        }

        return null;
    }

    private static string ReadString(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            return obj[propertyName]?.ToString()?.Trim() ?? "";
        }
        catch (ManagementException)
        {
            return "";
        }
    }

    private static int? ReadInt(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value is null)
                return null;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static uint? ReadUInt(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value is null)
                return null;
            return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<uint> ReadUIntArray(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            if (obj[propertyName] is not Array values)
                return Array.Empty<uint>();

            var result = new List<uint>();
            foreach (var value in values)
                result.Add(Convert.ToUInt32(value, CultureInfo.InvariantCulture));

            return result;
        }
        catch (Exception)
        {
            return Array.Empty<uint>();
        }
    }

    private static long? ReadLong(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value is null)
                return null;
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static float? ReadFloat(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value is null)
                return null;
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long? SumNonNull(params long?[] values)
    {
        long sum = 0;
        bool hasValue = false;

        foreach (var value in values)
        {
            if (!value.HasValue)
                continue;

            sum += value.Value;
            hasValue = true;
        }

        return hasValue ? sum : null;
    }

    private static string MapBusType(uint? busType) => busType switch
    {
        7 => "USB",
        8 => "RAID",
        11 => "SATA",
        12 => "SD",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        _ => busType.HasValue ? $"Bus {busType.Value}" : ""
    };

    private static string MapMediaType(uint? mediaType) => mediaType switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => mediaType.HasValue ? $"Media {mediaType.Value}" : ""
    };

    private static string MapHealthStatus(uint? healthStatus) => healthStatus switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        5 => "Unknown",
        _ => healthStatus.HasValue ? $"Health {healthStatus.Value}" : ""
    };

    private static string BuildStatusText(uint? healthStatus, IReadOnlyList<uint> operationalStatuses)
    {
        string health = MapHealthStatus(healthStatus);
        string operational = string.Join(", ", operationalStatuses
            .Select(MapOperationalStatus)
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        if (string.IsNullOrWhiteSpace(health))
            return operational;
        if (string.IsNullOrWhiteSpace(operational))
            return health;
        return $"{health} / {operational}";
    }

    private static string MapOperationalStatus(uint status) => status switch
    {
        2 => "OK",
        3 => "Degraded",
        4 => "Stressed",
        5 => "Predictive Failure",
        6 => "Error",
        7 => "Non-Recoverable Error",
        8 => "Starting",
        9 => "Stopping",
        11 => "In Service",
        15 => "Dormant",
        16 => "Supporting Entity in Error",
        532 => "Lost Communication",
        _ => status == 0 ? "" : $"Status {status}"
    };
}
