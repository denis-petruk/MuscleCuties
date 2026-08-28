using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    private int _lastCelebrationToken;

    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((WorkoutViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkoutViewModel.CelebrationToken) ||
            BindingContext is not WorkoutViewModel viewModel ||
            viewModel.CelebrationToken <= 0 ||
            viewModel.CelebrationToken == _lastCelebrationToken)
        {
            return;
        }

        _lastCelebrationToken = viewModel.CelebrationToken;
        MainThread.BeginInvokeOnMainThread(async () =>
            await SaluteOverlay.PlayAsync(viewModel.CelebrationIconSource));
    }
}
