using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HardwareMonitor;

public partial class MainWindow
{
    private Point _dragStartPoint;
    private bool _isDragging;

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not Border border) return;

        var pos = e.GetPosition(this);
        if (!_isDragging)
        {
            _dragStartPoint = pos;
            _isDragging = true;
            return;
        }

        var diff = pos - _dragStartPoint;
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = false;
            var cardId = border.Tag as string;
            if (cardId is null) return;

            var data = new DataObject("CardId", cardId);
            DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        }
    }

    private void Card_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CardId"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("CardId")
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CardId")) return;
        if (sender is not Border targetBorder) return;

        var sourceCardId = (string)e.Data.GetData("CardId");
        var targetCardId = targetBorder.Tag as string;

        if (sourceCardId == null || targetCardId == null || sourceCardId == targetCardId) return;
        if (_layoutVm is null) return;

        var fromIndex = -1;
        var toIndex = -1;
        for (int i = 0; i < _layoutVm.Cards.Count; i++)
        {
            if (_layoutVm.Cards[i].CardId == sourceCardId) fromIndex = i;
            if (_layoutVm.Cards[i].CardId == targetCardId) toIndex = i;
        }

        if (fromIndex >= 0 && toIndex >= 0)
        {
            _layoutVm.MoveCard(fromIndex, toIndex);
            ApplyLayout();
        }

        e.Handled = true;
    }
}
