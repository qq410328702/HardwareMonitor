using HardwareMonitor.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<DiskSnapshot> DiskSnapshots { get; } = new();
    public ObservableCollection<NetworkSnapshot> NetworkSnapshots { get; } = new();
    public ObservableCollection<ProcessInfo> TopProcesses { get; } = new();

    private ProcessSortMode _processSortMode = ProcessSortMode.ByCpu;
    public ProcessSortMode ProcessSortMode
    {
        get => _processSortMode;
        set => SetField(ref _processSortMode, value);
    }

    public void ToggleProcessSortMode()
    {
        ProcessSortMode = ProcessSortMode == ProcessSortMode.ByCpu
            ? ProcessSortMode.ByMemory
            : ProcessSortMode.ByCpu;
    }

    private void ApplyDiskSnapshots(List<DiskSnapshot> disks)
    {
        DiskSnapshots.Clear();
        foreach (var d in disks)
            DiskSnapshots.Add(d);
    }

    private void ApplyNetworkSnapshots(List<NetworkSnapshot> nets)
    {
        NetworkSnapshots.Clear();
        foreach (var n in nets)
            NetworkSnapshots.Add(n);
    }

    private void ApplyTopProcesses(List<ProcessInfo> procs)
    {
        TopProcesses.Clear();
        foreach (var p in procs)
            TopProcesses.Add(p);
    }
}
