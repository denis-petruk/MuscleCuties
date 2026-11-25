using System.ComponentModel;
using System.Windows.Input;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

/// <summary>
///     Sheet that records and lists training injuries; sizes itself to its content.
/// </summary>
public partial class InjuryLogModal : AnimatedModalView
{
    public static readonly BindableProperty CloseCommandProperty =
        BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(InjuryLogModal));

    private bool _hasContent;
    private readonly ViewModelSubscription<InjuryLogViewModel> _subscription;

    /// <summary>Hooks the form so its sheet height is recomputed as injuries are added or removed.</summary>
    public InjuryLogModal()
    {
        _subscription = ViewModelSubscriptionBinding.BindToControl<InjuryLogViewModel>(this,
            OnViewModelPropertyChanged);
        BindingContextChanged += (_, _) => UpdateSheetHeight();
    }

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
            $"[Performance][InjuryLogModal] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    /// <summary>Refits the sheet height after the size changes.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            UpdateSheetHeight();
    }

    /// <summary>Refreshes the sheet when the injury list state changes.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InjuryLogViewModel.IsBusy)
            or nameof(InjuryLogViewModel.ShowAddForm)
            or nameof(InjuryLogViewModel.ActiveInjuries)
            or nameof(InjuryLogViewModel.ClearedInjuries)
            or nameof(InjuryLogViewModel.IsHistoryExpanded)
            or nameof(InjuryLogViewModel.ShowEmptyState))
            Dispatcher.Dispatch(UpdateSheetHeight);
    }

    /// <summary>Asks the sheet to re-measure after the list changed, so the sheet grows with its content instead of</summary>
    /// scrolling inside a fixed height.</summary>
    private void UpdateSheetHeight()
    {
        if (!_hasContent || BindingContext is not InjuryLogViewModel vm)
            return;

        if (vm.ShowAddForm)
            BottomSheetSizing.Apply(SheetSurface, Height, BottomSheetSizing.MaximumSheetHeight);
        else
            BottomSheetSizing.ApplyContentFit(SheetSurface, Height);
    }
}
