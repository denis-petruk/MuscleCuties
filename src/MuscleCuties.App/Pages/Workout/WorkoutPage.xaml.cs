using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((WorkoutViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }
}
