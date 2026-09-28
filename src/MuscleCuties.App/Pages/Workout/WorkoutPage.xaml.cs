using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.App.Controls.Injury;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _viewModel;
    private Task? _injuryModalLoadTask;

    public WorkoutPage(WorkoutViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        // Bind before the page is presented so PageLoadingOverlay (which
        // binds to IPageLoadAware on BindingContext) is active from the
        // first render, gating the page behind the async load.
        this.BindWithTiming(vm, started);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (_viewModel.IsInjuryModalVisible)
            OnViewModelPropertyChanged(_viewModel, new PropertyChangedEventArgs(nameof(WorkoutViewModel.IsInjuryModalVisible)));
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.CloseInjuryModalCommand.Execute(null);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkoutViewModel.IsInjuryModalVisible))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(UpdateInjuryModalAsync);
        }
        catch
        {
            Trace.WriteLine("[WorkoutPage] Could not present injury modal.");
            _viewModel.CloseInjuryModalCommand.Execute(null);
        }
    }

    private async Task UpdateInjuryModalAsync()
    {
        if (!_viewModel.IsInjuryModalVisible && !InjuryModalLazy.HasLazyViewLoaded)
            return;

        if (_viewModel.IsInjuryModalVisible)
        {
            try
            {
                await (_injuryModalLoadTask ??= InjuryModalLazy.LoadIfNeededAsync(true).AsTask());
            }
            catch
            {
                _injuryModalLoadTask = null;
                throw;
            }
        }

        if (!InjuryModalLazy.HasLazyViewLoaded)
            return;

        var modal = (InjuryLogModal)InjuryModalLazy.Content;
        if (modal.BindingContext != _viewModel.InjuryLogVm)
            modal.BindingContext = _viewModel.InjuryLogVm;
        modal.CloseCommand = _viewModel.CloseInjuryModalCommand;
        modal.IsOpen = _viewModel.IsInjuryModalVisible;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsInjuryModalVisible)
            return base.OnBackButtonPressed();

        _viewModel.CloseInjuryModalCommand.Execute(null);
        return true;
    }
}
