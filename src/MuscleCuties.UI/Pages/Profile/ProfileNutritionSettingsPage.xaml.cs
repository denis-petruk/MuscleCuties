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

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm && vm.HasUnsavedChanges)
        {
            vm.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnCaloriesCommitted(object? sender, EventArgs e)
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm)
            vm.CommitCaloriesEdit();
    }

    private void OnMacroCommitted(object? sender, EventArgs e)
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm)
            vm.CommitMacroEdit();
    }
}
