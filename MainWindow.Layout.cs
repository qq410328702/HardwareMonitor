using HardwareMonitor.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace HardwareMonitor;

public partial class MainWindow
{
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

        var children = new List<UIElement>();
        foreach (UIElement child in CardPanel.Children)
            children.Add(child);
        CardPanel.Children.Clear();

        foreach (var card in _layoutVm.Cards)
        {
            if (_cardElements.TryGetValue(card.CardId, out var element))
            {
                element.Visibility = card.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                CardPanel.Children.Add(element);
            }
        }

        foreach (var kvp in _cardElements)
        {
            if (!CardPanel.Children.Contains(kvp.Value))
                CardPanel.Children.Add(kvp.Value);
        }
    }

    private void LayoutCard_Toggled(object sender, RoutedEventArgs e)
    {
        if (_layoutVm is null) return;
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not CardItemViewModel card) return;

        _layoutVm.SetCardVisibility(card.CardId, card.IsVisible);
        ApplyLayout();
    }
}
