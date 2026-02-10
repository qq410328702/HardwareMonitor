using System;
using System.IO;

namespace HardwareMonitor.Services;

public sealed class FileLogger : ILogger, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new();

    public FileLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, $"hw-monitor-{DateTime.Now:yyyyMMdd}.log");
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        lock (_writeLock)
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}");
    }

    public void Dispose() => _writer.Dispose();
}
