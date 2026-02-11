using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace HardwareMonitor.Services;

public enum DiskHealthStatus { Healthy, Warning, Critical }

public class DiskSnapshot
{
    public string Name { get; set; } = "";
    public float Temperature { get; set; }
    public float ReadSpeed { get; set; }  // MB/s
    public float WriteSpeed { get; set; } // MB/s
    public DiskHealthStatus Health { get; set; }
}

public interface IDiskMonitorService
{
    List<DiskSnapshot> GetDiskSnapshots();
}

public class DiskMonitorService : IDiskMonitorService
{
    private readonly HardwareService _hardwareService;
    private readonly ILogger _logger;

    public DiskMonitorService(HardwareService hardwareService, ILogger logger)
    {
        _hardwareService = hardwareService;
        _logger = logger;
    }

    public List<DiskSnapshot> GetDiskSnapshots()
    {
        var snapshots = new List<DiskSnapshot>();

        if (!_hardwareService.IsReady)
            return snapshots;

        var computer = _hardwareService.GetComputer();
        if (computer is null)
            return snapshots;

        foreach (var hw in computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Storage)
                continue;

            try
            {
                hw.Update();
                var disk = ReadStorage(hw);
                snapshots.Add(disk);
            }
            catch (Exception ex)
            {
                _logger.Warn($"磁盘 {hw.Name} 读取失败: {ex.Message}");
            }
        }

        return snapshots;
    }

    private static DiskSnapshot ReadStorage(IHardware hw)
    {
        var snapshot = new DiskSnapshot { Name = hw.Name };

        foreach (var sensor in hw.Sensors)
        {
            float value = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Temperature)
            {
                snapshot.Temperature = value;
            }
            else if (sensor.SensorType == SensorType.Throughput)
            {
                // LibreHardwareMonitor reports throughput in bytes/s
                if (sensor.Name.Contains("Read"))
                    snapshot.ReadSpeed = value / (1024f * 1024f); // Convert to MB/s
                else if (sensor.Name.Contains("Write"))
                    snapshot.WriteSpeed = value / (1024f * 1024f); // Convert to MB/s
            }
        }

        snapshot.Health = MapHealthStatus(snapshot.Temperature);
        return snapshot;
    }

    /// <summary>
    /// Maps disk temperature to a health status.
    /// Pure static function for easy property testing.
    /// > 60°C = Critical, > 50°C = Warning, &lt;= 50°C = Healthy
    /// </summary>
    public static DiskHealthStatus MapHealthStatus(float temperature)
    {
        if (temperature > 60f)
            return DiskHealthStatus.Critical;
        if (temperature > 50f)
            return DiskHealthStatus.Warning;
        return DiskHealthStatus.Healthy;
    }
}
