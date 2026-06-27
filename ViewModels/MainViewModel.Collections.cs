using HardwareMonitor.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        var incomingIds = disks
            .Where(d => !string.IsNullOrWhiteSpace(d.LayoutCardId))
            .Select(d => d.LayoutCardId)
            .ToHashSet();

        for (int i = DiskSnapshots.Count - 1; i >= 0; i--)
        {
            if (!incomingIds.Contains(DiskSnapshots[i].LayoutCardId))
                DiskSnapshots.RemoveAt(i);
        }

        for (int i = 0; i < disks.Count; i++)
        {
            var incoming = disks[i];
            var existingIndex = IndexOfDisk(incoming.LayoutCardId);
            if (existingIndex >= 0)
            {
                DiskSnapshots[existingIndex].UpdateFrom(incoming);
                if (existingIndex != i && i < DiskSnapshots.Count)
                    DiskSnapshots.Move(existingIndex, i);
            }
            else
            {
                DiskSnapshots.Insert(System.Math.Min(i, DiskSnapshots.Count), incoming);
            }
        }
    }

    private void ApplyNetworkSnapshots(List<NetworkSnapshot> nets)
    {
        ReplaceCollection(NetworkSnapshots, nets);
    }

    private void ApplyTopProcesses(List<ProcessInfo> procs)
    {
        ReplaceCollection(TopProcesses, procs);
    }

    private int IndexOfDisk(string layoutCardId)
    {
        for (int i = 0; i < DiskSnapshots.Count; i++)
            if (DiskSnapshots[i].LayoutCardId == layoutCardId)
                return i;
        return -1;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        int commonCount = System.Math.Min(target.Count, source.Count);
        for (int i = 0; i < commonCount; i++)
            target[i] = source[i];

        for (int i = commonCount; i < source.Count; i++)
            target.Add(source[i]);

        for (int i = target.Count - 1; i >= source.Count; i--)
            target.RemoveAt(i);
    }
}
