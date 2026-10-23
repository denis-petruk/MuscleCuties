using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

public partial class InjuryLogModal : AnimatedModalView
{
    private const double HeaderHeight = 88;
    private const double ContentPaddingHeight = 24;
    private const double EmptyCardHeight = 208;
    private const double ActiveHeadingHeight = 34;
    private const double InjuryRowHeight = 130;
    private const double HistoryHeaderHeight = 88;
    private const double LoadingContentHeight = 120;

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

        var preferredHeight = vm.ShowAddForm
            ? BottomSheetSizing.MaximumSheetHeight
            : HeaderHeight + ContentPaddingHeight + (vm.IsBusy || vm.IsLoadError
                ? LoadingContentHeight
                : vm.ActiveInjuries.Count == 0
                    ? EmptyCardHeight
                    : ActiveHeadingHeight + vm.ActiveInjuries.Count * InjuryRowHeight);

        if (vm.HasClearedInjuries)
            preferredHeight += HistoryHeaderHeight +
                               (vm.IsHistoryExpanded ? vm.ClearedInjuries.Count * InjuryRowHeight : 0);

        BottomSheetSizing.Apply(SheetSurface, Height, preferredHeight);
    }
}
