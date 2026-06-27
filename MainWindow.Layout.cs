using HardwareMonitor.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HardwareMonitor;

public partial class MainWindow
{
    private static readonly string[] HardwareCardOrder = ["cpu", "gpu", "memory"];
    private static readonly string[] ToolCardOrder = ["network", "process", "charts", "history", "alert", "layout"];

    private void BuildCardElementMap()
    {
        _cardElements = new Dictionary<string, Border>
        {
            ["cpu"] = Card_cpu,
            ["gpu"] = Card_gpu,
            ["memory"] = Card_memory,
            ["network"] = Card_network,
            ["process"] = Card_process,
            ["charts"] = Card_charts,
            ["history"] = Card_history,
            ["alert"] = Card_alert,
            ["layout"] = Card_layout
        };
    }

    private void ApplyLayout()
    {
        if (_layoutVm is null || _cardElements is null) return;

        HardwareCardPanel.Children.Clear();
        StorageCardPanel.Children.Clear();
        ToolsCardPanel.Children.Clear();

        var visibilityByCardId = _layoutVm.Cards
            .GroupBy(c => c.CardId)
            .ToDictionary(g => g.Key, g => g.First().IsVisible);
        var addedCardIds = new HashSet<string>();

        foreach (var cardId in HardwareCardOrder)
            AddCardToPanel(cardId, HardwareCardPanel, visibilityByCardId, addedCardIds);

        foreach (var cardId in GetDiskCardIds())
            AddCardToPanel(cardId, StorageCardPanel, visibilityByCardId, addedCardIds);

        foreach (var cardId in ToolCardOrder)
            AddCardToPanel(cardId, ToolsCardPanel, visibilityByCardId, addedCardIds);

        foreach (var cardId in _cardElements.Keys.Where(id => !addedCardIds.Contains(id)).ToList())
            AddCardToPanel(cardId, GetPanelForCard(cardId), visibilityByCardId, addedCardIds);

        HardwareGroup.Visibility = HasVisibleChildren(HardwareCardPanel) ? Visibility.Visible : Visibility.Collapsed;
        StorageGroup.Visibility = HasVisibleChildren(StorageCardPanel) ? Visibility.Visible : Visibility.Collapsed;
        ToolsGroup.Visibility = HasVisibleChildren(ToolsCardPanel) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LayoutCard_Toggled(object sender, RoutedEventArgs e)
    {
        if (_layoutVm is null) return;
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not CardItemViewModel card) return;

        _layoutVm.SetCardVisibility(card.CardId, card.IsVisible);
        ApplyLayout();
    }

    private IEnumerable<string> GetDiskCardIds()
    {
        var snapshotIds = Vm.DiskSnapshots
            .Where(d => !string.IsNullOrWhiteSpace(d.LayoutCardId))
            .GroupBy(d => d.LayoutCardId)
            .Select(g => g.Key);

        return snapshotIds
            .Concat(_cardElements?.Keys.Where(IsDiskCardId) ?? [])
            .Where(id => _cardElements?.ContainsKey(id) == true)
            .Distinct();
    }

    private void AddCardToPanel(
        string cardId,
        Panel panel,
        IReadOnlyDictionary<string, bool> visibilityByCardId,
        ISet<string> addedCardIds)
    {
        if (_cardElements is null || !_cardElements.TryGetValue(cardId, out var element))
            return;

        element.Visibility = visibilityByCardId.TryGetValue(cardId, out bool isVisible) && !isVisible
            ? Visibility.Collapsed
            : Visibility.Visible;
        panel.Children.Add(element);
        addedCardIds.Add(cardId);
    }

    private Panel GetPanelForCard(string cardId)
    {
        if (HardwareCardOrder.Contains(cardId))
            return HardwareCardPanel;
        if (IsDiskCardId(cardId))
            return StorageCardPanel;
        return ToolsCardPanel;
    }

    private static bool HasVisibleChildren(Panel panel) =>
        panel.Children.Cast<UIElement>().Any(child => child.Visibility == Visibility.Visible);

    private static bool IsDiskCardId(string cardId) => cardId.StartsWith("disk:");
}
