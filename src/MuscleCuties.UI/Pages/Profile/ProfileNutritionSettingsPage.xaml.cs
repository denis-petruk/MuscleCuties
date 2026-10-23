using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfileNutritionSettingsPage : ContentPage
{
    public ProfileNutritionSettingsPage(ProfileNutritionSettingsViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileNutritionSettingsViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
