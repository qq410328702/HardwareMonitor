using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
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
    private const int WriteBatchSize = 5;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private readonly List<PendingSnapshot> _pendingSnapshots = new();
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
        await _dbLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _pendingSnapshots.Add(PendingSnapshot.From(snapshot));
            if (_pendingSnapshots.Count >= WriteBatchSize)
                FlushPendingSnapshotsCore();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<List<SnapshotRecord>> QueryAsync(DateTime from, DateTime to)
    {
        var records = new List<SnapshotRecord>();

        await _dbLock.WaitAsync();
        try
        {
            FlushPendingSnapshotsCore();

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
        }
        finally
        {
            _dbLock.Release();
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
        await _dbLock.WaitAsync();
        try
        {
            FlushPendingSnapshotsCore();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM snapshots WHERE timestamp < @cutoff";

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _dbLock.Wait();
            try
            {
                FlushPendingSnapshotsCore();
                _connection.Dispose();
                _disposed = true;
            }
            finally
            {
                _dbLock.Release();
                _dbLock.Dispose();
            }
        }
    }

    private void FlushPendingSnapshotsCore()
    {
        if (_pendingSnapshots.Count == 0 || _disposed)
            return;

        using var transaction = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO snapshots (timestamp, cpu_temp, cpu_usage, gpu_temp, gpu_usage, mem_usage, total_power)
            VALUES (@timestamp, @cpuTemp, @cpuUsage, @gpuTemp, @gpuUsage, @memUsage, @totalPower)";

        var timestamp = cmd.Parameters.Add("@timestamp", SqliteType.Text);
        var cpuTemp = cmd.Parameters.Add("@cpuTemp", SqliteType.Real);
        var cpuUsage = cmd.Parameters.Add("@cpuUsage", SqliteType.Real);
        var gpuTemp = cmd.Parameters.Add("@gpuTemp", SqliteType.Real);
        var gpuUsage = cmd.Parameters.Add("@gpuUsage", SqliteType.Real);
        var memUsage = cmd.Parameters.Add("@memUsage", SqliteType.Real);
        var totalPower = cmd.Parameters.Add("@totalPower", SqliteType.Real);

        foreach (var snapshot in _pendingSnapshots)
        {
            timestamp.Value = snapshot.Timestamp.ToString("o", CultureInfo.InvariantCulture);
            cpuTemp.Value = snapshot.CpuTemp;
            cpuUsage.Value = snapshot.CpuUsage;
            gpuTemp.Value = snapshot.GpuTemp;
            gpuUsage.Value = snapshot.GpuUsage;
            memUsage.Value = snapshot.MemUsage;
            totalPower.Value = snapshot.TotalPower;
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        _pendingSnapshots.Clear();
    }

    private sealed class PendingSnapshot
    {
        public DateTime Timestamp { get; init; }
        public float CpuTemp { get; init; }
        public float CpuUsage { get; init; }
        public float GpuTemp { get; init; }
        public float GpuUsage { get; init; }
        public float MemUsage { get; init; }
        public float TotalPower { get; init; }

        public static PendingSnapshot From(HardwareSnapshot snapshot) => new()
        {
            Timestamp = DateTime.UtcNow,
            CpuTemp = snapshot.CpuTemp,
            CpuUsage = snapshot.CpuUsage,
            GpuTemp = snapshot.GpuTemp,
            GpuUsage = snapshot.GpuUsage,
            MemUsage = snapshot.MemUsage,
            TotalPower = snapshot.CpuPower + snapshot.GpuPower
        };
    }
}
