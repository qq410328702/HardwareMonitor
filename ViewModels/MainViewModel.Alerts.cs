using HardwareMonitor.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    private bool _isCpuTempAlert;
    private bool _isGpuTempAlert;
    private bool _isCpuUsageAlert;
    private bool _isGpuUsageAlert;
    private MetricType _newRuleMetric = MetricType.CpuTemp;
    private string _newRuleThreshold = "80";
    private CompareDirection _newRuleDirection = CompareDirection.Above;
    private string _alertRuleStatus = "";

    public ObservableCollection<AlertRule> AlertRules { get; } = new();
    public MetricType NewRuleMetric { get => _newRuleMetric; set => SetField(ref _newRuleMetric, value); }
    public string NewRuleThreshold { get => _newRuleThreshold; set => SetField(ref _newRuleThreshold, value); }
    public CompareDirection NewRuleDirection { get => _newRuleDirection; set => SetField(ref _newRuleDirection, value); }
    public string AlertRuleStatus { get => _alertRuleStatus; set => SetField(ref _alertRuleStatus, value); }
    public MetricType[] MetricTypes => (MetricType[])Enum.GetValues(typeof(MetricType));
    public CompareDirection[] CompareDirections => (CompareDirection[])Enum.GetValues(typeof(CompareDirection));
    public bool IsCpuTempAlert { get => _isCpuTempAlert; set => SetField(ref _isCpuTempAlert, value); }
    public bool IsGpuTempAlert { get => _isGpuTempAlert; set => SetField(ref _isGpuTempAlert, value); }
    public bool IsCpuUsageAlert { get => _isCpuUsageAlert; set => SetField(ref _isCpuUsageAlert, value); }
    public bool IsGpuUsageAlert { get => _isGpuUsageAlert; set => SetField(ref _isGpuUsageAlert, value); }

    public void AddAlertRule()
    {
        if (_alertEngine is null)
        {
            AlertRuleStatus = "告警引擎未初始化";
            return;
        }

        if (!float.TryParse(_newRuleThreshold, out var threshold))
        {
            AlertRuleStatus = "阈值必须为有效数字";
            return;
        }

        var rule = new AlertRule
        {
            Metric = _newRuleMetric,
            Threshold = threshold,
            Direction = _newRuleDirection
        };

        var result = _alertEngine.AddRule(rule);
        if (result.IsSuccess)
        {
            AlertRules.Add(rule);
            AlertRuleStatus = "规则已添加";
        }
        else
        {
            AlertRuleStatus = result.ErrorMessage ?? "添加失败";
        }
    }

    public void RemoveAlertRule(AlertRule rule)
    {
        if (_alertEngine is null) return;
        _alertEngine.RemoveRule(rule);
        AlertRules.Remove(rule);
        AlertRuleStatus = "规则已删除";
    }

    private void EvaluateAlerts(HardwareSnapshot snapshot)
    {
        if (_alertEngine is null) return;

        try
        {
            var results = _alertEngine.Evaluate(snapshot);

            IsCpuTempAlert = results.Any(r => r.Rule.Metric == MetricType.CpuTemp && r.IsTriggered)
                || (_isCpuTempAlert && results.Any(r => r.Rule.Metric == MetricType.CpuTemp && r.CurrentValue > r.Rule.Threshold));
            IsGpuTempAlert = results.Any(r => r.Rule.Metric == MetricType.GpuTemp && r.IsTriggered)
                || (_isGpuTempAlert && results.Any(r => r.Rule.Metric == MetricType.GpuTemp && r.CurrentValue > r.Rule.Threshold));
            IsCpuUsageAlert = results.Any(r => r.Rule.Metric == MetricType.CpuUsage && r.IsTriggered)
                || (_isCpuUsageAlert && results.Any(r => r.Rule.Metric == MetricType.CpuUsage && r.CurrentValue > r.Rule.Threshold));
            IsGpuUsageAlert = results.Any(r => r.Rule.Metric == MetricType.GpuUsage && r.IsTriggered)
                || (_isGpuUsageAlert && results.Any(r => r.Rule.Metric == MetricType.GpuUsage && r.CurrentValue > r.Rule.Threshold));

            foreach (var result in results)
            {
                bool exceeded = result.Rule.Direction == CompareDirection.Above
                    ? result.CurrentValue > result.Rule.Threshold
                    : result.CurrentValue < result.Rule.Threshold;

                if (!exceeded)
                {
                    switch (result.Rule.Metric)
                    {
                        case MetricType.CpuTemp: IsCpuTempAlert = false; break;
                        case MetricType.GpuTemp: IsGpuTempAlert = false; break;
                        case MetricType.CpuUsage: IsCpuUsageAlert = false; break;
                        case MetricType.GpuUsage: IsGpuUsageAlert = false; break;
                    }
                }
            }

            foreach (var result in results.Where(r => r.IsTriggered))
            {
                var metricName = result.Rule.Metric switch
                {
                    MetricType.CpuTemp => "CPU 温度",
                    MetricType.GpuTemp => "GPU 温度",
                    MetricType.CpuUsage => "CPU 使用率",
                    MetricType.GpuUsage => "GPU 使用率",
                    _ => result.Rule.Metric.ToString()
                };

                var unit = result.Rule.Metric is MetricType.CpuTemp or MetricType.GpuTemp ? "°C" : "%";
                var direction = result.Rule.Direction == CompareDirection.Above ? "超过" : "低于";

                var title = $"⚠ {metricName}告警";
                var text = $"{metricName}当前值 {result.CurrentValue:F1}{unit} {direction}阈值 {result.Rule.Threshold:F1}{unit}";

                _trayService?.ShowBalloonTip(title, text);
            }
        }
        catch (Exception)
        {
        }
    }
}
