using HardwareMonitor.ViewModels;
using System.Windows;

namespace HardwareMonitor;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.DiskSnapshots.CollectionChanged += DiskSnapshots_CollectionChanged;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDiskCards();
    }
}
