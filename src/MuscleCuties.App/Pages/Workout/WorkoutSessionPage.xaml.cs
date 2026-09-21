using MuscleCuties.App.Controls.Shared;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

[QueryProperty(nameof(WorkoutDayId), "workoutDayId")]
public partial class WorkoutSessionPage : ContentPage
{
    private readonly WorkoutSessionViewModel _viewModel;

    public WorkoutSessionPage(WorkoutSessionViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        BindingContext = _viewModel = vm;
    }

    public string WorkoutDayId
    {
        set => _ = _viewModel.LoadSession(int.TryParse(value, out var id) ? id : 0);
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        // The Train page is cached; make its next appearance reflect session logs.
        Handler?.MauiContext?.Services.GetService<WorkoutViewModel>()?.Invalidate();
        base.OnNavigatingFrom(args);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.BackCommand.ExecuteAsync(null);
        return true;
    }
}
