using System.Windows.Input;
using MuscleCuties.UI.ViewModels.Dashboard;

namespace MuscleCuties.UI.Controls.Dashboard;

/// <summary>
/// Presents the daily check-in as a sheet over the dashboard. The form keeps the routing
/// contract it had as a page: it loads the day's status on open and closes on completion.
/// </summary>
public partial class DailyCheckInModal : AnimatedModalView
{
    public static readonly BindableProperty CloseCommandProperty =
        BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(DailyCheckInModal));

    private bool _hasContent;

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Builds the modal's content the first time it opens.</summary>
    protected override void PrepareForOpen()
    {
        if (_hasContent)
            return;

        var started = Stopwatch.GetTimestamp();
        InitializeComponent();
        _hasContent = true;
        UpdateSheetHeight();
        Trace.WriteLine(
            $"[Performance][DailyCheckInModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    /// <summary>Refits the sheet height after the size changes.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            UpdateSheetHeight();
    }

    /// <summary>
    /// The check-in form is always taller than one screen, so the sheet opens at the shared
    /// ceiling rather than at an estimated content height.
    /// </summary>
    private void UpdateSheetHeight()
    {
        if (!_hasContent)
            return;

        // At accessibility sizes the form needs the full reviewed sheet height: the footer
        // stacks its actions and would otherwise leave only a sliver of the questions visible.
        var preferredHeight = AccessibilityDecoration.IsAccessibilityTextSize()
            ? BottomSheetSizing.MaximumSheetHeight
            : AppThemeResources.GetDouble("ModalSheetMaximumHeight");
        BottomSheetSizing.Apply(SheetSurface, Height, preferredHeight);
    }
}
