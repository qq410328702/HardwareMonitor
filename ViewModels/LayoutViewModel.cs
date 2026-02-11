using HardwareMonitor.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardwareMonitor.ViewModels;

public class CardItemViewModel : BaseViewModel
{
    private string _cardId = "";
    private int _order;
    private bool _isVisible = true;

    public string CardId { get => _cardId; set => SetField(ref _cardId, value); }
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
                Order = item.Order,
                IsVisible = item.IsVisible
            });
        }
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
}
