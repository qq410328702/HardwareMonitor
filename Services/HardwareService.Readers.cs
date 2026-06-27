using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HardwareMonitor.Services;

public partial class HardwareService
{
    private static void ReadCpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.CpuName = hw.Name;

        float powerPackage = 0, powerCores = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie"))
                    snap.CpuTemp = sensor.Value ?? 0;
                else if (snap.CpuTemp == 0)
                    snap.CpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                snap.CpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                if (v > 0)
                {
                    if (sensor.Name.Contains("Package"))
                        powerPackage = v;
                    else if (sensor.Name.Contains("Cores"))
                        powerCores = v;
                    else if (powerAny == 0)
                        powerAny = v;
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
                snap.CpuClock = sensor.Value ?? 0;
        }

        snap.CpuPower = powerPackage > 0 ? powerPackage
                       : powerCores > 0 ? powerCores
                       : powerAny;
    }

    private static void ReadGpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.GpuName = hw.Name;

        float powerBoard = 0, powerGpu = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"))
                    snap.GpuTemp = sensor.Value ?? 0;
                else if (snap.GpuTemp == 0)
                    snap.GpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                snap.GpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                string name = sensor.Name;
                if (v > 0)
                {
                    if (name.Contains("Board") || name.Contains("TDP") || name.Contains("Total"))
                        powerBoard = Math.Max(powerBoard, v);
                    else if (name.Contains("GPU") || name.Contains("Core"))
                        powerGpu = Math.Max(powerGpu, v);
                    else
                        powerAny = Math.Max(powerAny, v);
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                snap.GpuClock = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Used"))
                snap.GpuMemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Total"))
                snap.GpuMemTotal = sensor.Value ?? 0;
        }

        snap.GpuPower = powerBoard > 0 ? powerBoard
                       : powerGpu > 0 ? powerGpu
                       : powerAny;
    }

    private static void ReadMemory(IHardware hw, HardwareSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                snap.MemUsage = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                snap.MemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                snap.MemAvailable = sensor.Value ?? 0;
        }
    }

    private static void ReadPowerSummary(
        Computer computer,
        IHardware? primaryCpu,
        IHardware? primaryGpu,
        IHardware? primaryMemory,
        HardwareSnapshot snap)
    {
        var rawReadings = new List<RawPowerReading>();

        foreach (var hw in computer.Hardware)
        {
            if (!ReferenceEquals(hw, primaryCpu) &&
                !ReferenceEquals(hw, primaryGpu) &&
                !ReferenceEquals(hw, primaryMemory))
            {
                UpdateHardwareTree(hw);
            }

            CollectPowerSensors(hw, null, rawReadings);
        }

        ApplyPowerSummary(rawReadings, snap);
    }

    private static void UpdateHardwareTree(IHardware hw)
    {
        try { hw.Update(); }
        catch { }

        foreach (var child in hw.SubHardware)
            UpdateHardwareTree(child);
    }

    private static void CollectPowerSensors(
        IHardware hw,
        string? inheritedCategory,
        List<RawPowerReading> readings)
    {
        string category = GetPowerCategory(hw.HardwareType, inheritedCategory);

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType != SensorType.Power)
                continue;

            float watts = sensor.Value ?? 0f;
            if (watts <= 0.05f || float.IsNaN(watts) || float.IsInfinity(watts))
                continue;

            readings.Add(new RawPowerReading
            {
                Category = category,
                HardwareKey = hw.Identifier.ToString(),
                HardwareName = hw.Name,
                SensorName = sensor.Name,
                Watts = watts
            });
        }

        foreach (var child in hw.SubHardware)
            CollectPowerSensors(child, category, readings);
    }

    private static void ApplyPowerSummary(List<RawPowerReading> rawReadings, HardwareSnapshot snap)
    {
        var cpuReadings = SelectBestByHardware(rawReadings, "CPU", GetCpuPowerRank).ToList();
        var gpuReadings = SelectBestByHardware(rawReadings, "GPU", GetGpuPowerRank).ToList();
        var storageReadings = SelectBestByHardware(rawReadings, "存储", GetDefaultPowerRank).ToList();
        var memoryReadings = SelectBestByHardware(rawReadings, "内存", GetDefaultPowerRank).ToList();
        var psuReading = SelectBestPsuReading(rawReadings);

        if (cpuReadings.Count == 0 && snap.CpuPower > 0)
            cpuReadings.Add(RawPowerReading.FromFallback("CPU", "CPU", "功耗", snap.CpuPower));
        if (gpuReadings.Count == 0 && snap.GpuPower > 0)
            gpuReadings.Add(RawPowerReading.FromFallback("GPU", "GPU", "功耗", snap.GpuPower));

        if (cpuReadings.Count > 0)
            snap.CpuPower = cpuReadings.Sum(r => r.Watts);
        if (gpuReadings.Count > 0)
            snap.GpuPower = gpuReadings.Sum(r => r.Watts);

        bool hasCpu = cpuReadings.Count > 0;
        bool hasGpu = gpuReadings.Count > 0;
        var remainingReadings = rawReadings
            .Where(r => r.Category is not "CPU" and not "GPU" and not "存储" and not "内存" and not "电源")
            .Where(r => !LooksLikeCpuGpuDuplicate(r, hasCpu, hasGpu))
            .GroupBy(GetPowerReadingKey)
            .Select(g => g.OrderBy(GetDefaultPowerRank).ThenByDescending(r => r.Watts).First())
            .ToList();

        var included = new List<RawPowerReading>();
        var displayReadings = new List<PowerReading>();

        if (psuReading is not null)
        {
            included.Add(psuReading);
            displayReadings.Add(ToPowerReading(psuReading, true));

            foreach (var reading in cpuReadings
                         .Concat(gpuReadings)
                         .Concat(storageReadings)
                         .Concat(memoryReadings)
                         .Concat(remainingReadings))
            {
                displayReadings.Add(ToPowerReading(reading, false));
            }

            snap.PowerSourceText = "PSU 传感器读数";
        }
        else
        {
            included.AddRange(cpuReadings);
            included.AddRange(gpuReadings);
            included.AddRange(storageReadings);
            included.AddRange(memoryReadings);
            included.AddRange(remainingReadings);
            displayReadings.AddRange(included.Select(r => ToPowerReading(r, true)));
            snap.PowerSourceText = "已检测传感器汇总";
        }

        snap.TotalPower = included.Sum(r => r.Watts);
        if (snap.TotalPower <= 0)
        {
            snap.PowerUnavailableReason = "未检测到功耗传感器，可能需要管理员权限或硬件不支持";
            return;
        }

        foreach (var reading in displayReadings)
            reading.SharePercent = reading.Watts / snap.TotalPower * 100f;

        foreach (var reading in displayReadings
                     .OrderBy(r => GetCategoryOrder(r.Category))
                     .ThenByDescending(r => r.IsIncludedInTotal)
                     .ThenByDescending(r => r.Watts))
        {
            snap.PowerReadings.Add(reading);
        }
    }

    private static IEnumerable<RawPowerReading> SelectBestByHardware(
        IEnumerable<RawPowerReading> readings,
        string category,
        Func<RawPowerReading, int> ranker)
    {
        return readings
            .Where(r => r.Category == category)
            .GroupBy(r => NormalizeKey(r.HardwareKey))
            .Select(g => g.OrderBy(ranker).ThenByDescending(r => r.Watts).First());
    }

    private static RawPowerReading? SelectBestPsuReading(IEnumerable<RawPowerReading> readings)
    {
        return readings
            .Where(r => r.Category == "电源")
            .OrderBy(GetPsuPowerRank)
            .ThenByDescending(r => r.Watts)
            .FirstOrDefault();
    }

    private static PowerReading ToPowerReading(RawPowerReading reading, bool isIncludedInTotal) => new()
    {
        Category = reading.Category,
        Name = BuildPowerDisplayName(reading),
        Watts = reading.Watts,
        IsIncludedInTotal = isIncludedInTotal
    };

    private static string GetPowerCategory(HardwareType type, string? inheritedCategory)
    {
        if (type == HardwareType.Cpu)
            return "CPU";
        if (type is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia)
            return "GPU";
        if (type == HardwareType.Storage)
            return "存储";
        if (type == HardwareType.Memory)
            return "内存";
        if (type == HardwareType.Motherboard)
            return "主板";
        if (type == HardwareType.Psu)
            return "电源";
        if (type == HardwareType.Battery)
            return "电池";

        return inheritedCategory ?? "其它";
    }

    private static int GetCpuPowerRank(RawPowerReading reading)
    {
        string name = reading.SensorName;
        if (Contains(name, "Package")) return 0;
        if (Contains(name, "Total")) return 1;
        if (Contains(name, "Cores")) return 2;
        return 10;
    }

    private static int GetGpuPowerRank(RawPowerReading reading)
    {
        string name = reading.SensorName;
        if (Contains(name, "Board") || Contains(name, "Total") || Contains(name, "TDP")) return 0;
        if (Contains(name, "GPU") || Contains(name, "Core")) return 1;
        return 10;
    }

    private static int GetPsuPowerRank(RawPowerReading reading)
    {
        string name = reading.SensorName;
        if (Contains(name, "Total")) return 0;
        if (Contains(name, "Input")) return 1;
        if (Contains(name, "Output")) return 2;
        return 10;
    }

    private static int GetDefaultPowerRank(RawPowerReading reading)
    {
        string name = reading.SensorName;
        if (Contains(name, "Total") || Contains(name, "Power")) return 0;
        return 10;
    }

    private static int GetCategoryOrder(string category) => category switch
    {
        "电源" => 0,
        "CPU" => 1,
        "GPU" => 2,
        "存储" => 3,
        "内存" => 4,
        "主板" => 5,
        "电池" => 6,
        _ => 8
    };

    private static bool LooksLikeCpuGpuDuplicate(RawPowerReading reading, bool hasCpu, bool hasGpu)
    {
        string text = $"{reading.HardwareName} {reading.SensorName}";
        if (hasCpu && (Contains(text, "CPU") || Contains(text, "Processor") ||
                       Contains(text, "Package") || Contains(text, "Core")))
        {
            return true;
        }

        if (hasGpu && (Contains(text, "GPU") || Contains(text, "Graphics") ||
                       Contains(text, "Video") || Contains(text, "PCIe")))
        {
            return true;
        }

        return false;
    }

    private static string BuildPowerDisplayName(RawPowerReading reading)
    {
        if (string.IsNullOrWhiteSpace(reading.SensorName))
            return reading.HardwareName;

        if (Contains(reading.HardwareName, reading.SensorName) ||
            Contains(reading.SensorName, reading.HardwareName))
        {
            return reading.SensorName;
        }

        return $"{reading.HardwareName} - {reading.SensorName}";
    }

    private static string GetPowerReadingKey(RawPowerReading reading) =>
        $"{NormalizeKey(reading.Category)}|{NormalizeKey(reading.HardwareKey)}|{NormalizeKey(reading.SensorName)}";

    private static string NormalizeKey(string value) =>
        value.Trim().ToUpperInvariant();

    private static bool Contains(string value, string pattern) =>
        value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private sealed class RawPowerReading
    {
        public string Category { get; init; } = "";
        public string HardwareKey { get; init; } = "";
        public string HardwareName { get; init; } = "";
        public string SensorName { get; init; } = "";
        public float Watts { get; init; }

        public static RawPowerReading FromFallback(
            string category,
            string hardwareName,
            string sensorName,
            float watts) => new()
        {
            Category = category,
            HardwareKey = hardwareName,
            HardwareName = hardwareName,
            SensorName = sensorName,
            Watts = watts
        };
    }
}
