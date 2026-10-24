using System.ComponentModel;
using Microsoft.Maui.Accessibility;

namespace MuscleCuties.UI.Controls.Shared;

public sealed class ScreenReaderStatusAnnouncementBehavior : Behavior<Label>
{
    protected override void OnAttachedTo(Label bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.PropertyChanged += OnLabelPropertyChanged;
    }

    protected override void OnDetachingFrom(Label bindable)
    {
        bindable.PropertyChanged -= OnLabelPropertyChanged;
        base.OnDetachingFrom(bindable);
    }

    private static void OnLabelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Label.Text) || sender is not Label label)
            return;

        var status = label.Text;
        if (string.IsNullOrWhiteSpace(status))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsVisibleInTree(label) && string.Equals(label.Text, status, StringComparison.Ordinal))
                SemanticScreenReader.Default.Announce(status);
        });
    }

    private static bool IsVisibleInTree(VisualElement visualElement)
    {
        for (Element? current = visualElement; current is not null; current = current.Parent)
        {
            if (current is VisualElement ancestor && !ancestor.IsVisible)
                return false;
        }

        return true;
    }
}
