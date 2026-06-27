using System;
using System.Collections.Generic;

namespace HardwareMonitor.Services;

public sealed class ElectricityCostSnapshot
{
    public decimal CurrentRateYuanPerKwh { get; init; }
    public string CurrentRateName { get; init; } = "平段";
    public bool IsUsingFallbackRate { get; init; }
    public string StatusText { get; init; } = "";
    public decimal TodayKwh { get; init; }
    public decimal TodayCostYuan { get; init; }
    public decimal MonthKwh { get; init; }
    public decimal MonthCostYuan { get; init; }
    public decimal TotalKwh { get; init; }
    public decimal TotalCostYuan { get; init; }
    public IReadOnlyList<ElectricityTariffPeriod> TariffPeriods { get; init; } = Array.Empty<ElectricityTariffPeriod>();
}
