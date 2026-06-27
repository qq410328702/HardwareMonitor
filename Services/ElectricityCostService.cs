using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HardwareMonitor.Services;

public sealed class ElectricityCostService : IDisposable
{
    public const decimal DefaultRateYuanPerKwh = 0.60m;

    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _sync = new();
    private readonly string _statePath;
    private ElectricityCostState _state;
    private DateTimeOffset? _lastSampleTime;
    private float _lastWatts;
    private bool _dirty;
    private DateTimeOffset _lastSaveTime = DateTimeOffset.MinValue;
    private string _persistWarning = "";

    public ElectricityCostService(string? statePath = null)
    {
        _statePath = statePath ?? GetDefaultStatePath();
        _state = LoadState();

        lock (_sync)
        {
            EnsureCurrentPeriods(DateTime.Now);
        }
    }

    public ElectricityCostSnapshot RecordPowerSample(float watts, DateTimeOffset now)
    {
        lock (_sync)
        {
            EnsureCurrentPeriods(now.LocalDateTime);

            var currentWatts = Math.Max(0f, watts);
            if (currentWatts > 0 && _lastSampleTime.HasValue)
            {
                var elapsedSeconds = (now - _lastSampleTime.Value).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    if (elapsedSeconds > 5)
                        elapsedSeconds = 1;

                    var averageWatts = ((decimal)_lastWatts + (decimal)currentWatts) / 2m;
                    if (averageWatts > 0)
                    {
                        var kwh = averageWatts * (decimal)elapsedSeconds / 3_600_000m;
                        AddUsage(kwh, ResolveTariff(now.LocalDateTime).Rate);
                    }
                }
            }

            _lastWatts = currentWatts;
            _lastSampleTime = now;
            SaveIfDue(now);
            return CreateSnapshot(now.LocalDateTime);
        }
    }

    public ElectricityCostSnapshot GetSnapshot(DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.Now;
        lock (_sync)
        {
            EnsureCurrentPeriods(timestamp.LocalDateTime);
            return CreateSnapshot(timestamp.LocalDateTime);
        }
    }

    public void UpdateTariffPeriods(IEnumerable<ElectricityTariffPeriod> periods)
    {
        lock (_sync)
        {
            _state.TariffPeriods = periods.Select(ClonePeriod).ToList();
            if (_state.TariffPeriods.Count == 0)
                _state.TariffPeriods = CreateDefaultTariffPeriods();

            _persistWarning = "";
            _dirty = true;
            SaveLocked();
        }
    }

    public void RestoreDefaultTariffPeriods()
    {
        lock (_sync)
        {
            _state.TariffPeriods = CreateDefaultTariffPeriods();
            _persistWarning = "";
            _dirty = true;
            SaveLocked();
        }
    }

    public void Flush()
    {
        lock (_sync)
        {
            SaveLocked();
        }
    }

    public void Dispose()
    {
        Flush();
    }

    public static List<ElectricityTariffPeriod> CreateDefaultTariffPeriods()
    {
        return
        [
            new ElectricityTariffPeriod
            {
                Name = "平段",
                StartTime = "00:00",
                EndTime = "24:00",
                RateYuanPerKwh = DefaultRateYuanPerKwh
            }
        ];
    }

    private static string GetDefaultStatePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "electricity.json");
    }

    private ElectricityCostState LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
                return ElectricityCostState.CreateDefault();

            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<ElectricityCostState>(json, JsonOptions);
            if (state is null)
                return ElectricityCostState.CreateDefault();

            state.TariffPeriods ??= CreateDefaultTariffPeriods();
            if (state.TariffPeriods.Count == 0)
                state.TariffPeriods = CreateDefaultTariffPeriods();

            return state;
        }
        catch
        {
            _persistWarning = "电费配置读取失败，当前使用默认电价";
            return ElectricityCostState.CreateDefault();
        }
    }

    private void EnsureCurrentPeriods(DateTime localNow)
    {
        var dayKey = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var monthKey = localNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        if (!string.Equals(_state.CurrentDay, dayKey, StringComparison.Ordinal))
        {
            _state.CurrentDay = dayKey;
            _state.TodayKwh = 0;
            _state.TodayCostYuan = 0;
            _dirty = true;
        }

        if (!string.Equals(_state.CurrentMonth, monthKey, StringComparison.Ordinal))
        {
            _state.CurrentMonth = monthKey;
            _state.MonthKwh = 0;
            _state.MonthCostYuan = 0;
            _dirty = true;
        }
    }

    private void AddUsage(decimal kwh, decimal rate)
    {
        if (kwh <= 0)
            return;

        var cost = kwh * rate;
        _state.TodayKwh += kwh;
        _state.TodayCostYuan += cost;
        _state.MonthKwh += kwh;
        _state.MonthCostYuan += cost;
        _state.TotalKwh += kwh;
        _state.TotalCostYuan += cost;
        _dirty = true;
    }

    private ElectricityCostSnapshot CreateSnapshot(DateTime localNow)
    {
        var tariff = ResolveTariff(localNow);
        var status = !string.IsNullOrWhiteSpace(_persistWarning)
            ? _persistWarning
            : tariff.StatusText;

        return new ElectricityCostSnapshot
        {
            CurrentRateName = tariff.Name,
            CurrentRateYuanPerKwh = tariff.Rate,
            IsUsingFallbackRate = tariff.IsFallback,
            StatusText = status,
            TodayKwh = _state.TodayKwh,
            TodayCostYuan = _state.TodayCostYuan,
            MonthKwh = _state.MonthKwh,
            MonthCostYuan = _state.MonthCostYuan,
            TotalKwh = _state.TotalKwh,
            TotalCostYuan = _state.TotalCostYuan,
            TariffPeriods = _state.TariffPeriods.Select(ClonePeriod).ToList()
        };
    }

    private TariffResolution ResolveTariff(DateTime localNow)
    {
        if (_state.TariffPeriods.Count == 0)
            return FallbackTariff("未配置电价，当前使用默认电价");

        var currentMinute = localNow.Hour * 60 + localNow.Minute;
        foreach (var period in _state.TariffPeriods)
        {
            if (!TryValidatePeriod(period, out var startMinute, out var endMinute))
                return FallbackTariff("电价配置无效，当前使用默认电价");

            if (IsMinuteInPeriod(currentMinute, startMinute, endMinute))
            {
                return new TariffResolution(
                    string.IsNullOrWhiteSpace(period.Name) ? "当前" : period.Name.Trim(),
                    period.RateYuanPerKwh,
                    false,
                    "应用运行期间累计");
            }
        }

        return FallbackTariff("当前时间未匹配电价时段，已使用默认电价");
    }

    private static TariffResolution FallbackTariff(string statusText)
    {
        return new TariffResolution("默认", DefaultRateYuanPerKwh, true, statusText);
    }

    private static bool TryValidatePeriod(ElectricityTariffPeriod period, out int startMinute, out int endMinute)
    {
        startMinute = 0;
        endMinute = 0;

        if (period.RateYuanPerKwh < 0)
            return false;

        if (!TryParseMinute(period.StartTime, false, out startMinute))
            return false;

        if (!TryParseMinute(period.EndTime, true, out endMinute))
            return false;

        return startMinute != endMinute;
    }

    private static bool TryParseMinute(string text, bool allowEndOfDay, out int minute)
    {
        minute = 0;
        text = (text ?? "").Trim();

        if (allowEndOfDay && string.Equals(text, "24:00", StringComparison.Ordinal))
        {
            minute = 24 * 60;
            return true;
        }

        if (!TimeSpan.TryParseExact(
                text,
                ["h\\:mm", "hh\\:mm"],
                CultureInfo.InvariantCulture,
                out var time))
            return false;

        if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            return false;

        minute = (int)time.TotalMinutes;
        return true;
    }

    private static bool IsMinuteInPeriod(int minute, int startMinute, int endMinute)
    {
        if (startMinute < endMinute)
            return minute >= startMinute && minute < endMinute;

        return minute >= startMinute || minute < endMinute;
    }

    private void SaveIfDue(DateTimeOffset now)
    {
        if (!_dirty)
            return;

        if (now - _lastSaveTime >= SaveInterval)
            SaveLocked();
    }

    private void SaveLocked()
    {
        if (!_dirty)
            return;

        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = _statePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_state, JsonOptions));
            File.Move(tempPath, _statePath, true);
            _dirty = false;
            _lastSaveTime = DateTimeOffset.Now;
            _persistWarning = "";
        }
        catch
        {
            _persistWarning = "电费数据暂时无法保存";
        }
    }

    private static ElectricityTariffPeriod ClonePeriod(ElectricityTariffPeriod period)
    {
        return new ElectricityTariffPeriod
        {
            Name = period.Name,
            StartTime = period.StartTime,
            EndTime = period.EndTime,
            RateYuanPerKwh = period.RateYuanPerKwh
        };
    }

    private sealed record TariffResolution(string Name, decimal Rate, bool IsFallback, string StatusText);

    private sealed class ElectricityCostState
    {
        public string CurrentDay { get; set; } = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public string CurrentMonth { get; set; } = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        public decimal TodayKwh { get; set; }
        public decimal TodayCostYuan { get; set; }
        public decimal MonthKwh { get; set; }
        public decimal MonthCostYuan { get; set; }
        public decimal TotalKwh { get; set; }
        public decimal TotalCostYuan { get; set; }
        public List<ElectricityTariffPeriod> TariffPeriods { get; set; } = CreateDefaultTariffPeriods();

        public static ElectricityCostState CreateDefault()
        {
            return new ElectricityCostState
            {
                TariffPeriods = CreateDefaultTariffPeriods()
            };
        }
    }
}
