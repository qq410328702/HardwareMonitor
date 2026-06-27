using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HardwareMonitor;

public partial class MainWindow
{
    private bool _diskCardRefreshQueued;
    private readonly Dictionary<string, Border> _dynamicDiskCards = new();

    private void DiskSnapshots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueDiskCardRefresh();
    }

    private void QueueDiskCardRefresh()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(QueueDiskCardRefresh);
            return;
        }

        if (_diskCardRefreshQueued)
            return;

        _diskCardRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _diskCardRefreshQueued = false;
            RefreshDiskCards();
        }));
    }

    private void RefreshDiskCards()
    {
        if (_cardElements is null)
            return;

        var disks = Vm.DiskSnapshots
            .Where(d => !string.IsNullOrWhiteSpace(d.LayoutCardId))
            .GroupBy(d => d.LayoutCardId)
            .Select(g => g.First())
            .ToList();

        if (disks.Count == 0 && _dynamicDiskCards.Count == 0)
            return;

        var currentIds = disks.Select(d => d.LayoutCardId).ToHashSet();
        foreach (var removedId in _dynamicDiskCards.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            _dynamicDiskCards.Remove(removedId);
            _cardElements.Remove(removedId);
        }

        var registrations = new List<(string CardId, string DisplayName)>();
        foreach (var disk in disks)
        {
            string cardId = disk.LayoutCardId;
            if (!_dynamicDiskCards.TryGetValue(cardId, out var card))
            {
                card = CreateDiskCard(cardId);
                _dynamicDiskCards[cardId] = card;
                _cardElements[cardId] = card;
            }

            card.Tag = cardId;
            card.DataContext = disk;
            if (card.Child is ContentControl content)
                content.Content = disk;

            registrations.Add((cardId, disk.LayoutDisplayName));
        }

        _layoutVm?.RegisterDynamicCards(registrations);
        ApplyLayout();
    }

    private Border CreateDiskCard(string cardId)
    {
        var content = new ContentControl
        {
            ContentTemplate = (DataTemplate)FindResource("DiskCardContentTemplate")
        };

        var card = new Border
        {
            Tag = cardId,
            Background = Brushes.Transparent,
            Margin = new Thickness(4),
            Child = content
        };

        return card;
    }
}
