using System.Windows;

namespace HardwareMonitor;

public partial class MainWindow
{
    private void SaveElectricityTariffs_Click(object sender, RoutedEventArgs e)
    {
        Vm.TrySaveElectricityTariffs();
    }

    private void RestoreElectricityTariffs_Click(object sender, RoutedEventArgs e)
    {
        Vm.RestoreDefaultElectricityTariffs();
    }

    private void AddElectricityTariff_Click(object sender, RoutedEventArgs e)
    {
        Vm.AddElectricityTariffPeriod();
    }

    private void RemoveElectricityTariff_Click(object sender, RoutedEventArgs e)
    {
        Vm.RemoveElectricityTariffPeriod((sender as FrameworkElement)?.DataContext);
    }
}
