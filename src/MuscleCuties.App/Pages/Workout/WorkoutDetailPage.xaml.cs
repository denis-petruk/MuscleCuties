using MuscleCuties.App.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutDetailPage : ContentPage
{
    public WorkoutDetailPage(WorkoutDetailViewModelBag bag)
    {
        InitializeComponent();
        BindingContext = bag.Current ?? throw new InvalidOperationException("WorkoutDetailViewModel not set in bag before navigating.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((WorkoutDetailViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
