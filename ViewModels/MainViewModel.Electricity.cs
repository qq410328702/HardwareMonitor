using HardwareMonitor.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    private string _currentElectricityRateDisplay = "平段 0.60 元/kWh";
    private string _electricityStatus = "应用运行期间累计";
    private string _electricityConfigMessage = "";
    private string _todayElectricityKwhDisplay = "0.000 kWh";
    private string _todayElectricityCostDisplay = "0.00 元";
    private string _monthElectricityKwhDisplay = "0.000 kWh";
    private string _monthElectricityCostDisplay = "0.00 元";
    private string _totalElectricityKwhDisplay = "0.000 kWh";
    private string _totalElectricityCostDisplay = "0.00 元";

    public ObservableCollection<ElectricityTariffPeriodViewModel> ElectricityTariffPeriods { get; } = new();

    public string CurrentElectricityRateDisplay { get => _currentElectricityRateDisplay; set => SetField(ref _currentElectricityRateDisplay, value); }
    public string ElectricityStatus { get => _electricityStatus; set => SetField(ref _electricityStatus, value); }
    public string ElectricityConfigMessage { get => _electricityConfigMessage; set => SetField(ref _electricityConfigMessage, value); }
    public string TodayElectricityKwhDisplay { get => _todayElectricityKwhDisplay; set => SetField(ref _todayElectricityKwhDisplay, value); }
    public string TodayElectricityCostDisplay { get => _todayElectricityCostDisplay; set => SetField(ref _todayElectricityCostDisplay, value); }
    public string MonthElectricityKwhDisplay { get => _monthElectricityKwhDisplay; set => SetField(ref _monthElectricityKwhDisplay, value); }
    public string MonthElectricityCostDisplay { get => _monthElectricityCostDisplay; set => SetField(ref _monthElectricityCostDisplay, value); }
    public string TotalElectricityKwhDisplay { get => _totalElectricityKwhDisplay; set => SetField(ref _totalElectricityKwhDisplay, value); }
    public string TotalElectricityCostDisplay { get => _totalElectricityCostDisplay; set => SetField(ref _totalElectricityCostDisplay, value); }

    public void SaveElectricityTariffs()
    {
        var periods = ElectricityTariffPeriods
            .Select((period, index) => (period, index))
            .Select(item =>
            {
                if (!item.period.TryToModel(out var model, out var error))
                    throw new ElectricityTariffException($"第 {item.index + 1} 行：{error}");
                return model;
            })
            .ToList();

        if (periods.Count == 0)
        {
            ElectricityConfigMessage = "至少保留一个电价时段";
            return;
        }

        _electricityCost.UpdateTariffPeriods(periods);
        ApplyElectricitySnapshot(_electricityCost.GetSnapshot());
        RefreshElectricityTariffEditors(_electricityCost.GetSnapshot().TariffPeriods);
        ElectricityConfigMessage = "电价已保存，后续费用按新电价累计";
    }

    public void TrySaveElectricityTariffs()
    {
        try
        {
            SaveElectricityTariffs();
        }
        catch (ElectricityTariffException ex)
        {
            ElectricityConfigMessage = ex.Message;
        }
    }

    public void RestoreDefaultElectricityTariffs()
    {
        _electricityCost.RestoreDefaultTariffPeriods();
        var snapshot = _electricityCost.GetSnapshot();
        ApplyElectricitySnapshot(snapshot);
        RefreshElectricityTariffEditors(snapshot.TariffPeriods);
        ElectricityConfigMessage = "已恢复默认电价";
    }

    public void AddElectricityTariffPeriod()
    {
        ElectricityTariffPeriods.Add(new ElectricityTariffPeriodViewModel
        {
            Name = "新时段",
            StartTime = "00:00",
            EndTime = "24:00",
            RateYuanPerKwh = ElectricityCostService.DefaultRateYuanPerKwh.ToString("F2", CultureInfo.InvariantCulture)
        });
        ElectricityConfigMessage = "";
    }

    public void RemoveElectricityTariffPeriod(object? item)
    {
        if (ElectricityTariffPeriods.Count <= 1)
        {
            ElectricityConfigMessage = "至少保留一个电价时段";
            return;
        }

        if (item is ElectricityTariffPeriodViewModel period)
            ElectricityTariffPeriods.Remove(period);
    }

    private void InitializeElectricityCost()
    {
        var snapshot = _electricityCost.GetSnapshot();
        ApplyElectricitySnapshot(snapshot);
        RefreshElectricityTariffEditors(snapshot.TariffPeriods);
    }

    private void UpdateElectricityCost(HardwareSnapshot snapshot)
    {
        var watts = snapshot.HasPowerReadings ? snapshot.TotalPower : 0f;
        ApplyElectricitySnapshot(_electricityCost.RecordPowerSample(watts, System.DateTimeOffset.Now));
    }

    private void ApplyElectricitySnapshot(ElectricityCostSnapshot snapshot)
    {
        CurrentElectricityRateDisplay = $"{snapshot.CurrentRateName} {FormatMoney(snapshot.CurrentRateYuanPerKwh)} 元/kWh";
        ElectricityStatus = snapshot.StatusText;
        TodayElectricityKwhDisplay = FormatKwh(snapshot.TodayKwh);
        TodayElectricityCostDisplay = FormatCost(snapshot.TodayCostYuan);
        MonthElectricityKwhDisplay = FormatKwh(snapshot.MonthKwh);
        MonthElectricityCostDisplay = FormatCost(snapshot.MonthCostYuan);
        TotalElectricityKwhDisplay = FormatKwh(snapshot.TotalKwh);
        TotalElectricityCostDisplay = FormatCost(snapshot.TotalCostYuan);
    }

    private void RefreshElectricityTariffEditors(System.Collections.Generic.IReadOnlyList<ElectricityTariffPeriod> periods)
    {
        ElectricityTariffPeriods.Clear();
        foreach (var period in periods)
            ElectricityTariffPeriods.Add(ElectricityTariffPeriodViewModel.FromModel(period));
    }

    private static string FormatKwh(decimal value) => $"{value.ToString("F3", CultureInfo.InvariantCulture)} kWh";
    private static string FormatCost(decimal value) => $"{FormatMoney(value)} 元";
    private static string FormatMoney(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private sealed class ElectricityTariffException(string message) : System.Exception(message);
}
