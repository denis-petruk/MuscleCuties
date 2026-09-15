using System.ComponentModel;
using System.Diagnostics;
using MuscleCuties.App.Controls.Shared;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _viewModel;
    private int _lastCelebrationToken;

    public WorkoutPage(WorkoutViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (BindingContext is null)
            BindingContext = _viewModel;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        this.BeginPageLoad(() => _viewModel.LoadDataCommand.ExecuteAsync(null));
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnNavigatedFrom(args);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkoutViewModel.CelebrationToken) ||
            BindingContext is not WorkoutViewModel viewModel ||
            viewModel.CelebrationToken <= 0 ||
            viewModel.CelebrationToken == _lastCelebrationToken)
            return;

        _lastCelebrationToken = viewModel.CelebrationToken;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await SaluteOverlay.LoadIfNeededAsync(true);
                await ((PhaseSaluteOverlay)SaluteOverlay.Content).PlayAsync(viewModel.CelebrationIconSource);
            });
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[WorkoutPage] Could not play completion animation: {exception}");
        }
    }
}
