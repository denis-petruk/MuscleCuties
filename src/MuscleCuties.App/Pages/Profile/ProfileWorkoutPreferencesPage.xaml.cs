using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileWorkoutPreferencesPage : ContentPage
{
    public ProfileWorkoutPreferencesPage(ProfileWorkoutPreferencesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileWorkoutPreferencesViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
