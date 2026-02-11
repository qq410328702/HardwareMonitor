using System;
using System.Collections.Generic;

namespace HardwareMonitor.Services;

public enum MetricType { CpuTemp, GpuTemp, CpuUsage, GpuUsage }

public enum CompareDirection { Above, Below }

public class AlertRule
{
    public MetricType Metric { get; set; }
    public float Threshold { get; set; }
    public CompareDirection Direction { get; set; }
}

public class AlertResult
{
    public AlertRule Rule { get; set; } = null!;
    public float CurrentValue { get; set; }
    public bool IsTriggered { get; set; }
}

public class ValidationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}

public interface IAlertEngine
{
    ValidationResult AddRule(AlertRule rule);
    void RemoveRule(AlertRule rule);
    IReadOnlyList<AlertRule> GetRules();
    IReadOnlyList<AlertResult> Evaluate(HardwareSnapshot snapshot);
    void ClearAlertState(AlertRule rule);
}

public class AlertEngine : IAlertEngine
{
    private readonly List<AlertRule> _rules = new();
    private readonly Dictionary<AlertRule, DateTime> _lastTriggered = new();
    private readonly HashSet<AlertRule> _activeAlerts = new();
    private readonly TimeSpan _cooldown;

    public AlertEngine(TimeSpan? cooldown = null)
    {
        _cooldown = cooldown ?? TimeSpan.FromSeconds(60);
    }

    public ValidationResult AddRule(AlertRule rule)
    {
        var (min, max) = rule.Metric switch
        {
            MetricType.CpuTemp or MetricType.GpuTemp => (0f, 150f),
            MetricType.CpuUsage or MetricType.GpuUsage => (0f, 100f),
            _ => (0f, 100f)
        };

        if (rule.Threshold < min || rule.Threshold > max)
        {
            return ValidationResult.Failure(
                $"Threshold {rule.Threshold} is out of valid range [{min}, {max}] for {rule.Metric}.");
        }

        _rules.Add(rule);
        return ValidationResult.Success();
    }

    public void RemoveRule(AlertRule rule)
    {
        _rules.Remove(rule);
        _lastTriggered.Remove(rule);
        _activeAlerts.Remove(rule);
    }

    public IReadOnlyList<AlertRule> GetRules() => _rules.AsReadOnly();

    public IReadOnlyList<AlertResult> Evaluate(HardwareSnapshot snapshot)
    {
        if (_rules.Count == 0)
            return Array.Empty<AlertResult>();

        var results = new List<AlertResult>();
        var now = DateTime.UtcNow;

        foreach (var rule in _rules)
        {
            float value = GetMetricValue(snapshot, rule.Metric);
            bool exceeded = rule.Direction == CompareDirection.Above
                ? value > rule.Threshold
                : value < rule.Threshold;

            if (exceeded)
            {
                bool coolingDown = _lastTriggered.TryGetValue(rule, out var lastTime)
                    && (now - lastTime) < _cooldown;

                if (!coolingDown && !_activeAlerts.Contains(rule))
                {
                    // First trigger or after cooldown reset
                    _lastTriggered[rule] = now;
                    _activeAlerts.Add(rule);
                    results.Add(new AlertResult { Rule = rule, CurrentValue = value, IsTriggered = true });
                }
                else
                {
                    // Still exceeded but in cooldown or already active
                    results.Add(new AlertResult { Rule = rule, CurrentValue = value, IsTriggered = false });
                }
            }
            else
            {
                // Value recovered to normal range — clear alert state
                if (_activeAlerts.Contains(rule))
                {
                    _activeAlerts.Remove(rule);
                    _lastTriggered.Remove(rule);
                }

                results.Add(new AlertResult { Rule = rule, CurrentValue = value, IsTriggered = false });
            }
        }

        return results;
    }

    public void ClearAlertState(AlertRule rule)
    {
        _activeAlerts.Remove(rule);
        _lastTriggered.Remove(rule);
    }

    private static float GetMetricValue(HardwareSnapshot snapshot, MetricType metric) => metric switch
    {
        MetricType.CpuTemp => snapshot.CpuTemp,
        MetricType.GpuTemp => snapshot.GpuTemp,
        MetricType.CpuUsage => snapshot.CpuUsage,
        MetricType.GpuUsage => snapshot.GpuUsage,
        _ => 0f
    };
}

