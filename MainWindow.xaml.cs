using HardwareMonitor.ViewModels;
using HardwareMonitor.Services;
using System.Windows;

namespace HardwareMonitor;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;
    private readonly UpdateService _updateService;

    public MainWindow(MainViewModel vm, UpdateService updateService)
    {
        _updateService = updateService;
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
