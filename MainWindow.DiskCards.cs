using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HardwareMonitor.Services;

namespace HardwareMonitor;

public partial class MainWindow
{
    private bool _diskCardRefreshQueued;
    private readonly Dictionary<string, Border> _dynamicDiskCards = new();

    private void DiskSnapshots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DiskSnapshot disk in e.OldItems.OfType<DiskSnapshot>())
                disk.PropertyChanged -= DiskSnapshot_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (DiskSnapshot disk in e.NewItems.OfType<DiskSnapshot>())
                disk.PropertyChanged += DiskSnapshot_PropertyChanged;
        }

        QueueDiskCardRefresh();
    }

    private void DiskSnapshot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiskSnapshot.StorageGroupKey))
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
        var disks = Vm.DiskSnapshots
            .Where(d => !string.IsNullOrWhiteSpace(d.LayoutCardId))
            .GroupBy(d => d.LayoutCardId)
            .Select(g => g.First())
            .ToList();

        var currentIds = disks.Select(d => d.LayoutCardId).ToHashSet();
        if (disks.Count == 0 && _dynamicDiskCards.Count == 0)
        {
            RefreshStorageGroupVisibility();
            return;
        }

        foreach (var removedId in _dynamicDiskCards.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            RemoveDiskCardFromParent(_dynamicDiskCards[removedId]);
            _dynamicDiskCards.Remove(removedId);
        }

        foreach (var disk in disks)
        {
            string cardId = disk.LayoutCardId;
            if (!_dynamicDiskCards.TryGetValue(cardId, out var card))
            {
                card = CreateDiskCard(cardId);
                _dynamicDiskCards[cardId] = card;
            }

            card.Tag = cardId;
            card.DataContext = disk;
            if (card.Child is ContentControl content)
                content.Content = disk;

            var targetPanel = GetStorageCardPanel(disk.StorageGroupKey);
            if (!targetPanel.Children.Contains(card))
            {
                RemoveDiskCardFromParent(card);
                targetPanel.Children.Add(card);
            }
        }

        RefreshStorageGroupVisibility();
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

        card.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(DiskCardButton_Click));

        return card;
    }

    private WrapPanel GetStorageCardPanel(string groupKey) => groupKey switch
    {
        DiskTemperaturePolicy.NvmeGroupKey => NvmeStorageCardPanel,
        DiskTemperaturePolicy.SataSsdGroupKey => SataSsdStorageCardPanel,
        DiskTemperaturePolicy.HddGroupKey => HddStorageCardPanel,
        _ => OtherStorageCardPanel
    };

    private static void RemoveDiskCardFromParent(Border card)
    {
        if (card.Parent is Panel panel)
            panel.Children.Remove(card);
    }

    private void RefreshStorageGroupVisibility()
    {
        NvmeStorageGroup.Visibility = NvmeStorageCardPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SataSsdStorageGroup.Visibility = SataSsdStorageCardPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        HddStorageGroup.Visibility = HddStorageCardPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        OtherStorageGroup.Visibility = OtherStorageCardPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        StorageGroup.Visibility = _dynamicDiskCards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DiskCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (FindAncestorButton(e.OriginalSource as DependencyObject) is not { } button ||
            button.Name != "BadSectorCheckButton" ||
            button.Tag is not DiskSnapshot disk)
        {
            return;
        }

        var dialog = new DiskBadSectorDialog(disk)
        {
            Owner = this
        };
        dialog.ShowDialog();
        e.Handled = true;
    }

    private static Button? FindAncestorButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button button)
                return button;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
