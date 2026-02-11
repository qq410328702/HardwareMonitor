using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace HardwareMonitor.Services;

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public float CpuPercent { get; set; }
    public float MemoryMB { get; set; }
}

public enum ProcessSortMode { ByCpu, ByMemory }

public interface IProcessMonitorService
{
    List<ProcessInfo> GetTopProcesses(int topN = 10, ProcessSortMode sort = ProcessSortMode.ByCpu);
}

public class ProcessMonitorService : IProcessMonitorService
{
    private readonly ILogger _logger;
    private readonly Dictionary<int, (TimeSpan cpuTime, DateTime sampleTime)> _previous = new();

    public ProcessMonitorService(ILogger logger)
    {
        _logger = logger;
    }

    public List<ProcessInfo> GetTopProcesses(int topN = 10, ProcessSortMode sort = ProcessSortMode.ByCpu)
    {
        var all = new List<ProcessInfo>();
        Process[] processes;

        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            _logger.Error("获取进程列表失败", ex);
            return all;
        }

        var now = DateTime.UtcNow;
        int processorCount = Environment.ProcessorCount;

        foreach (var proc in processes)
        {
            try
            {
                var pid = proc.Id;
                var name = proc.ProcessName;
                var cpuTime = proc.TotalProcessorTime;
                var memoryMB = (float)(proc.WorkingSet64 / (1024.0 * 1024.0));

                float cpuPercent = 0f;
                if (_previous.TryGetValue(pid, out var prev))
                {
                    double elapsed = (now - prev.sampleTime).TotalMilliseconds;
                    if (elapsed > 0)
                    {
                        double cpuDelta = (cpuTime - prev.cpuTime).TotalMilliseconds;
                        cpuPercent = (float)(cpuDelta / elapsed / processorCount * 100.0);
                        cpuPercent = Math.Max(0f, cpuPercent);
                    }
                }

                _previous[pid] = (cpuTime, now);

                all.Add(new ProcessInfo
                {
                    Pid = pid,
                    Name = name,
                    CpuPercent = cpuPercent,
                    MemoryMB = memoryMB
                });
            }
            catch (Win32Exception)
            {
                // Permission error — silently skip
            }
            catch (InvalidOperationException)
            {
                // Process exited during access — silently skip
            }
            catch (Exception ex)
            {
                _logger.Warn($"进程信息采集失败: {ex.Message}");
            }
            finally
            {
                proc.Dispose();
            }
        }

        return SortAndTake(all, topN, sort);
    }

    /// <summary>
    /// Sorts a list of ProcessInfo by the specified mode (descending) and takes the top N entries.
    /// Pure static function for easy property testing.
    /// </summary>
    /// <param name="all">Full list of process info entries</param>
    /// <param name="topN">Maximum number of entries to return</param>
    /// <param name="sort">Sort mode: ByCpu sorts by CpuPercent descending, ByMemory sorts by MemoryMB descending</param>
    /// <returns>Sorted list with at most topN entries</returns>
    public static List<ProcessInfo> SortAndTake(List<ProcessInfo> all, int topN, ProcessSortMode sort)
    {
        if (all == null || all.Count == 0 || topN <= 0)
            return new List<ProcessInfo>();

        IEnumerable<ProcessInfo> sorted = sort switch
        {
            ProcessSortMode.ByCpu => all.OrderByDescending(p => p.CpuPercent),
            ProcessSortMode.ByMemory => all.OrderByDescending(p => p.MemoryMB),
            _ => all.OrderByDescending(p => p.CpuPercent)
        };

        return sorted.Take(topN).ToList();
    }
}
