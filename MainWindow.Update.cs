using System.Windows;

namespace HardwareMonitor;

public partial class MainWindow
{
    public void BeginManualUpdateCheck()
    {
        var dialog = new UpdateDialog(_updateService, null, true)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BeginManualUpdateCheck();
    }
}
