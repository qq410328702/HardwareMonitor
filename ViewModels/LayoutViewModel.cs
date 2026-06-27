using HardwareMonitor.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardwareMonitor.ViewModels;

public class CardItemViewModel : BaseViewModel
{
    private string _cardId = "";
    private string _displayName = "";
    private int _order;
    private bool _isVisible = true;

    public string CardId { get => _cardId; set => SetField(ref _cardId, value); }
    public string DisplayName
    {
        get => string.IsNullOrWhiteSpace(_displayName)
            ? LayoutViewModel.GetDefaultDisplayName(_cardId)
            : _displayName;
        set => SetField(ref _displayName, value);
    }
    public int Order { get => _order; set => SetField(ref _order, value); }
    public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value); }
}

public class LayoutViewModel : BaseViewModel
{
    private readonly ILayoutPersistenceService _persistenceService;

    public ObservableCollection<CardItemViewModel> Cards { get; } = new();

    public LayoutViewModel(ILayoutPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
        LoadLayout();
    }

    private void LoadLayout()
    {
        var config = _persistenceService.Load();
        if (config.Cards.Count == 0)
            config = LayoutPersistenceService.CreateDefaultLayout();

        Cards.Clear();
        foreach (var item in config.Cards.OrderBy(c => c.Order))
        {
            Cards.Add(new CardItemViewModel
            {
                CardId = item.CardId,
                DisplayName = GetDefaultDisplayName(item.CardId),
                Order = item.Order,
                IsVisible = item.IsVisible
            });
        }
    }

    public void RegisterDynamicCards(IEnumerable<(string CardId, string DisplayName)> cards)
    {
        var dynamicCards = cards
            .Where(c => IsDynamicDiskCard(c.CardId))
            .GroupBy(c => c.CardId)
            .Select(g => g.First())
            .ToList();

        var currentIds = dynamicCards.Select(c => c.CardId).ToHashSet();
        bool changed = false;
        bool legacyDiskWasVisible = true;
        int legacyDiskIndex = IndexOf("disk");

        if (legacyDiskIndex >= 0)
        {
            legacyDiskWasVisible = Cards[legacyDiskIndex].IsVisible;
            Cards.RemoveAt(legacyDiskIndex);
            changed = true;
        }

        for (int i = Cards.Count - 1; i >= 0; i--)
        {
            if (IsDynamicDiskCard(Cards[i].CardId) && !currentIds.Contains(Cards[i].CardId))
            {
                Cards.RemoveAt(i);
                changed = true;
            }
        }

        int insertIndex = GetDynamicInsertIndex(legacyDiskIndex);
        foreach (var dynamicCard in dynamicCards)
        {
            var existing = Cards.FirstOrDefault(c => c.CardId == dynamicCard.CardId);
            if (existing is not null)
            {
                existing.DisplayName = dynamicCard.DisplayName;
                continue;
            }

            Cards.Insert(insertIndex, new CardItemViewModel
            {
                CardId = dynamicCard.CardId,
                DisplayName = dynamicCard.DisplayName,
                IsVisible = legacyDiskWasVisible
            });
            insertIndex++;
            changed = true;
        }

        if (!changed)
            return;

        NormalizeOrder();
        SaveLayout();
    }

    public void MoveCard(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Cards.Count) return;
        if (toIndex < 0 || toIndex >= Cards.Count) return;
        if (fromIndex == toIndex) return;

        Cards.Move(fromIndex, toIndex);

        // Update order values
        for (int i = 0; i < Cards.Count; i++)
            Cards[i].Order = i;

        SaveLayout();
    }

    public void SetCardVisibility(string cardId, bool isVisible)
    {
        var card = Cards.FirstOrDefault(c => c.CardId == cardId);
        if (card is null) return;

        card.IsVisible = isVisible;
        SaveLayout();
    }

    private void SaveLayout()
    {
        var config = new LayoutConfig();
        foreach (var card in Cards)
        {
            config.Cards.Add(new CardLayoutItem
            {
                CardId = card.CardId,
                Order = card.Order,
                IsVisible = card.IsVisible
            });
        }
        _persistenceService.Save(config);
    }

    private int IndexOf(string cardId)
    {
        for (int i = 0; i < Cards.Count; i++)
            if (Cards[i].CardId == cardId)
                return i;
        return -1;
    }

    private int GetDynamicInsertIndex(int legacyDiskIndex)
    {
        if (legacyDiskIndex >= 0)
            return System.Math.Min(legacyDiskIndex, Cards.Count);

        for (int i = Cards.Count - 1; i >= 0; i--)
            if (IsDynamicDiskCard(Cards[i].CardId))
                return i + 1;

        int networkIndex = IndexOf("network");
        return networkIndex >= 0 ? networkIndex : Cards.Count;
    }

    private void NormalizeOrder()
    {
        for (int i = 0; i < Cards.Count; i++)
            Cards[i].Order = i;
    }

    private static bool IsDynamicDiskCard(string cardId) => cardId.StartsWith("disk:");

    public static string GetDefaultDisplayName(string cardId)
    {
        if (IsDynamicDiskCard(cardId))
            return "磁盘";

        return cardId switch
        {
            "cpu" => "CPU 温度",
            "gpu" => "GPU 温度",
            "memory" => "内存使用率",
            "disk" => "磁盘监控",
            "network" => "网络监控",
            "process" => "进程监控",
            "charts" => "图表趋势",
            "history" => "历史数据",
            "alert" => "告警规则",
            "layout" => "布局设置",
            _ => cardId
        };
    }
}
