namespace HardwareMonitor.Services;

public sealed class ElectricityTariffPeriod
{
    public string Name { get; set; } = "平段";
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "24:00";
    public decimal RateYuanPerKwh { get; set; } = 0.60m;

    public ElectricityTariffPeriod Clone()
    {
        return new ElectricityTariffPeriod
        {
            Name = Name,
            StartTime = StartTime,
            EndTime = EndTime,
            RateYuanPerKwh = RateYuanPerKwh
        };
    }
}
