using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace HardwareMonitor.Services;

public class NetworkSnapshot
{
    public string AdapterName { get; set; } = "";
    public float UploadSpeedKBps { get; set; }
    public float DownloadSpeedKBps { get; set; }
    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }
}

public interface INetworkMonitorService
{
    List<NetworkSnapshot> GetNetworkSnapshots();
}

public class NetworkMonitorService : INetworkMonitorService
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, (long sent, long received, DateTime time)> _previous = new();
    private readonly Dictionary<string, (long totalSent, long totalReceived)> _cumulative = new();

    public NetworkMonitorService(ILogger logger)
    {
        _logger = logger;
    }

    public List<NetworkSnapshot> GetNetworkSnapshots()
    {
        var snapshots = new List<NetworkSnapshot>();

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception ex)
        {
            _logger.Error("枚举网络适配器失败", ex);
            return snapshots;
        }

        var now = DateTime.UtcNow;

        foreach (var ni in interfaces)
        {
            // Filter out loopback and inactive interfaces
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            try
            {
                var stats = ni.GetIPv4Statistics();
                var name = ni.Name;
                long currentSent = stats.BytesSent;
                long currentReceived = stats.BytesReceived;

                float uploadSpeed = 0f;
                float downloadSpeed = 0f;

                if (_previous.TryGetValue(name, out var prev))
                {
                    double elapsed = (now - prev.time).TotalSeconds;
                    if (elapsed > 0)
                    {
                        uploadSpeed = CalculateSpeed(prev.sent, currentSent, elapsed);
                        downloadSpeed = CalculateSpeed(prev.received, currentReceived, elapsed);
                    }

                    // Accumulate cumulative bytes using deltas
                    long deltaSent = CalculateDelta(prev.sent, currentSent);
                    long deltaReceived = CalculateDelta(prev.received, currentReceived);

                    if (_cumulative.TryGetValue(name, out var cum))
                    {
                        _cumulative[name] = (cum.totalSent + deltaSent, cum.totalReceived + deltaReceived);
                    }
                    else
                    {
                        _cumulative[name] = (deltaSent, deltaReceived);
                    }
                }
                else
                {
                    // First sample: initialize cumulative to zero
                    if (!_cumulative.ContainsKey(name))
                    {
                        _cumulative[name] = (0, 0);
                    }
                }

                _previous[name] = (currentSent, currentReceived, now);

                long totalSent = 0;
                long totalReceived = 0;
                if (_cumulative.TryGetValue(name, out var cum2))
                {
                    totalSent = cum2.totalSent;
                    totalReceived = cum2.totalReceived;
                }

                snapshots.Add(new NetworkSnapshot
                {
                    AdapterName = name,
                    UploadSpeedKBps = uploadSpeed,
                    DownloadSpeedKBps = downloadSpeed,
                    TotalBytesSent = totalSent,
                    TotalBytesReceived = totalReceived
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"网络适配器 {ni.Name} 读取失败: {ex.Message}");
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Calculates the byte delta between two counter values, handling 32-bit overflow wraparound.
    /// If current &lt; previous, assumes the counter wrapped around uint.MaxValue.
    /// </summary>
    private static long CalculateDelta(long previous, long current)
    {
        if (current >= previous)
            return current - previous;

        // 32-bit overflow wraparound
        return (long)uint.MaxValue - previous + current;
    }

    /// <summary>
    /// Calculates network speed in KB/s from two byte counter values and elapsed time.
    /// Pure static function for easy property testing.
    /// Handles 32-bit counter overflow wraparound: if current &lt; previous,
    /// delta = uint.MaxValue - previous + current.
    /// </summary>
    /// <param name="previous">Previous byte count</param>
    /// <param name="current">Current byte count</param>
    /// <param name="elapsedSeconds">Time elapsed between samples in seconds (must be positive)</param>
    /// <returns>Speed in KB/s, always non-negative</returns>
    public static float CalculateSpeed(long previous, long current, double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
            return 0f;

        long delta = CalculateDelta(previous, current);

        return (float)(delta / elapsedSeconds / 1024.0);
    }
}
