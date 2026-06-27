using HardwareMonitor.Services;
using System.Globalization;

namespace HardwareMonitor.ViewModels;

public sealed class ElectricityTariffPeriodViewModel : BaseViewModel
{
    private string _name = "平段";
    private string _startTime = "00:00";
    private string _endTime = "24:00";
    private string _rateYuanPerKwh = "0.60";

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string StartTime { get => _startTime; set => SetField(ref _startTime, value); }
    public string EndTime { get => _endTime; set => SetField(ref _endTime, value); }
    public string RateYuanPerKwh { get => _rateYuanPerKwh; set => SetField(ref _rateYuanPerKwh, value); }

    public static ElectricityTariffPeriodViewModel FromModel(ElectricityTariffPeriod model)
    {
        return new ElectricityTariffPeriodViewModel
        {
            Name = model.Name,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            RateYuanPerKwh = model.RateYuanPerKwh.ToString("F2", CultureInfo.InvariantCulture)
        };
    }

    public bool TryToModel(out ElectricityTariffPeriod model, out string error)
    {
        model = new ElectricityTariffPeriod();
        error = "";

        if (!TryParseRate(RateYuanPerKwh, out var rate) || rate < 0)
        {
            error = "电价必须是大于或等于 0 的数字";
            return false;
        }

        model = new ElectricityTariffPeriod
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "电价" : Name.Trim(),
            StartTime = string.IsNullOrWhiteSpace(StartTime) ? "00:00" : StartTime.Trim(),
            EndTime = string.IsNullOrWhiteSpace(EndTime) ? "24:00" : EndTime.Trim(),
            RateYuanPerKwh = rate
        };
        return true;
    }

    private static bool TryParseRate(string text, out decimal rate)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out rate)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out rate);
    }
}
