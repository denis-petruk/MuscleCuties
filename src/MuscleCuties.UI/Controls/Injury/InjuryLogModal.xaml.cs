using System.ComponentModel;
using System.Windows.Input;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

public partial class InjuryLogModal : AnimatedModalView
{
    public static readonly BindableProperty CloseCommandProperty =
        BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(InjuryLogModal));

    private bool _hasContent;
    private InjuryLogViewModel? _subscribedViewModel;

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

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

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            UpdateSheetHeight();
    }

    protected override void OnBindingContextChanged()
    {
        DetachViewModel();
        base.OnBindingContextChanged();
        AttachViewModel();
        UpdateSheetHeight();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            DetachViewModel();
        else
            AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (BindingContext is not InjuryLogViewModel vm || ReferenceEquals(vm, _subscribedViewModel))
            return;

        DetachViewModel();
        _subscribedViewModel = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
    }

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
