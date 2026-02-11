using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace HardwareMonitor.Services;

public class SnapshotRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public float CpuTemp { get; set; }
    public float CpuUsage { get; set; }
    public float GpuTemp { get; set; }
    public float GpuUsage { get; set; }
    public float MemUsage { get; set; }
    public float TotalPower { get; set; }
}

public interface IDataStorageService : IDisposable
{
    Task SaveSnapshotAsync(HardwareSnapshot snapshot);
    Task<List<SnapshotRecord>> QueryAsync(DateTime from, DateTime to);
    Task ExportCsvAsync(DateTime from, DateTime to, string filePath);
    Task CleanupOldDataAsync(int retentionDays = 30);
}

public class DataStorageService : IDataStorageService
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public DataStorageService(string? connectionString = null)
    {
        if (connectionString == null)
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HardwareMonitor", "data");
            Directory.CreateDirectory(dataDir);
            connectionString = $"Data Source={Path.Combine(dataDir, "monitor.db")}";
        }

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                cpu_temp REAL NOT NULL,
                cpu_usage REAL NOT NULL,
                gpu_temp REAL NOT NULL,
                gpu_usage REAL NOT NULL,
                mem_usage REAL NOT NULL,
                total_power REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_snapshots_timestamp ON snapshots(timestamp);";
        cmd.ExecuteNonQuery();
    }

    public async Task SaveSnapshotAsync(HardwareSnapshot snapshot)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO snapshots (timestamp, cpu_temp, cpu_usage, gpu_temp, gpu_usage, mem_usage, total_power)
            VALUES (@timestamp, @cpuTemp, @cpuUsage, @gpuTemp, @gpuUsage, @memUsage, @totalPower)";

        cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@cpuTemp", snapshot.CpuTemp);
        cmd.Parameters.AddWithValue("@cpuUsage", snapshot.CpuUsage);
        cmd.Parameters.AddWithValue("@gpuTemp", snapshot.GpuTemp);
        cmd.Parameters.AddWithValue("@gpuUsage", snapshot.GpuUsage);
        cmd.Parameters.AddWithValue("@memUsage", snapshot.MemUsage);
        cmd.Parameters.AddWithValue("@totalPower", snapshot.CpuPower + snapshot.GpuPower);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SnapshotRecord>> QueryAsync(DateTime from, DateTime to)
    {
        var records = new List<SnapshotRecord>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, timestamp, cpu_temp, cpu_usage, gpu_temp, gpu_usage, mem_usage, total_power
            FROM snapshots
            WHERE timestamp BETWEEN @from AND @to
            ORDER BY timestamp ASC";

        cmd.Parameters.AddWithValue("@from", from.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@to", to.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new SnapshotRecord
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                CpuTemp = reader.GetFloat(2),
                CpuUsage = reader.GetFloat(3),
                GpuTemp = reader.GetFloat(4),
                GpuUsage = reader.GetFloat(5),
                MemUsage = reader.GetFloat(6),
                TotalPower = reader.GetFloat(7)
            });
        }

        return records;
    }

    public async Task ExportCsvAsync(DateTime from, DateTime to, string filePath)
    {
        var records = await QueryAsync(from, to);
        var csv = CsvSerializer.Serialize(records);
        await File.WriteAllTextAsync(filePath, csv);
    }

    public async Task CleanupOldDataAsync(int retentionDays = 30)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM snapshots WHERE timestamp < @cutoff";

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
