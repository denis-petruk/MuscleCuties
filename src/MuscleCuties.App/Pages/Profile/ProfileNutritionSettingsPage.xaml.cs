using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileNutritionSettingsPage : ContentPage
{
    public ProfileNutritionSettingsPage(ProfileNutritionSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileNutritionSettingsViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
