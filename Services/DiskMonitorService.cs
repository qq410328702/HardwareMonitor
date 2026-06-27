using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HardwareMonitor.Services;

public interface IDiskMonitorService
{
    List<DiskSnapshot> GetDiskSnapshots();
}

public class DiskMonitorService : IDiskMonitorService
{
    private const int LifetimeRefreshSeconds = 30;

    private readonly HardwareService _hardwareService;
    private readonly ILogger _logger;
    private readonly DiskLifetimeReader _lifetimeReader = new();
    private readonly object _lifetimeLock = new();
    private IReadOnlyList<DiskLifetimeInfo> _lifetimeCache = Array.Empty<DiskLifetimeInfo>();
    private DateTime _lastLifetimeRefreshUtc = DateTime.MinValue;
    private bool _loggedReliabilityAccessWarning;

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

        var lifetimeInfos = GetCachedLifetimeInfos();

        foreach (var hw in computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Storage)
                continue;

            try
            {
                hw.Update();
                var disk = DiskSensorParser.ReadStorage(hw);
                var lifetimeInfo = DiskLifetimeReader.FindBestMatch(hw, disk, lifetimeInfos);
                lifetimeInfo?.ApplyTo(disk);

                disk.LayoutCardId = DiskLifetimeReader.CreateLayoutCardId(hw, disk.Name, lifetimeInfo);
                DiskHealthMapper.FinalizeLifetimeStatus(disk);
                snapshots.Add(disk);
            }
            catch (Exception ex)
            {
                _logger.Warn($"磁盘 {hw.Name} 读取失败: {ex.Message}");
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Maps disk temperature to a health status.
    /// Pure static function for easy property testing.
    /// &gt; 60°C = Critical, &gt; 50°C = Warning, &lt;= 50°C = Healthy
    /// </summary>
    public static DiskHealthStatus MapHealthStatus(float temperature) =>
        DiskHealthMapper.MapTemperature(temperature);

    private IReadOnlyList<DiskLifetimeInfo> GetCachedLifetimeInfos()
    {
        lock (_lifetimeLock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastLifetimeRefreshUtc).TotalSeconds < LifetimeRefreshSeconds)
                return _lifetimeCache;

            _lifetimeCache = _lifetimeReader.Read();
            _lastLifetimeRefreshUtc = now;

            if (!_loggedReliabilityAccessWarning &&
                _lifetimeCache.Any(i => !string.IsNullOrWhiteSpace(i.ReliabilityUnavailableReason)))
            {
                _logger.Warn("磁盘寿命可靠性计数不可读，可能需要管理员权限或设备不支持 SMART。");
                _loggedReliabilityAccessWarning = true;
            }

            return _lifetimeCache;
        }
    }
}
