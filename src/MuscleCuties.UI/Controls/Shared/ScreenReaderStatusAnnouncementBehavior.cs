using System.ComponentModel;
using Microsoft.Maui.Accessibility;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Announces a status label's text to the screen reader when it changes.
/// </summary>
public sealed class ScreenReaderStatusAnnouncementBehavior : Behavior<Label>
{
    /// <summary>Starts announcing the label's text changes.</summary>
    protected override void OnAttachedTo(Label bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.PropertyChanged += OnLabelPropertyChanged;
    }

    /// <summary>Stops announcing the label's text changes.</summary>
    protected override void OnDetachingFrom(Label bindable)
    {
        bindable.PropertyChanged -= OnLabelPropertyChanged;
        base.OnDetachingFrom(bindable);
    }

    /// <summary>Announces the label again when its text changes.</summary>
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

    /// <summary>Whether the announced element is still on screen, so a message is not read out for a control that has</summary>
    /// already gone.</summary>
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
