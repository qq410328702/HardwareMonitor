using HardwareMonitor.Services;
using HardwareMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace HardwareMonitor;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;
    private LayoutViewModel? _layoutVm;

    // Map CardId -> named Border element
    private Dictionary<string, Border>? _cardElements;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.DiskSnapshots.CollectionChanged += DiskSnapshots_CollectionChanged;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    public void SetLayoutViewModel(LayoutViewModel layoutVm)
    {
        _layoutVm = layoutVm;
        LayoutCardList.ItemsSource = _layoutVm.Cards;
        if (IsLoaded)
            ApplyLayout();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BuildCardElementMap();
        RefreshDiskCards();
        ApplyLayout();
    }
}
