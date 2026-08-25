using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App.Pages.Workout;

public partial class WorkoutPage : ContentPage
{
    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((WorkoutViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
