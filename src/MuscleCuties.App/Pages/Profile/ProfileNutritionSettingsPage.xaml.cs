using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileNutritionSettingsPage : ContentPage
{
    public ProfileNutritionSettingsPage(ProfileNutritionSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileNutritionSettingsViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
