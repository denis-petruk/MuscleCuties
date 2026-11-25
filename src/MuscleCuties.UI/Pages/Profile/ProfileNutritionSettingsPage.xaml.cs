using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Nutrition settings: dietary preferences and per-meal targets.
/// </summary>
public partial class ProfileNutritionSettingsPage : ContentPage
{
    /// <summary>Initializes the page and its view model.</summary>
    public ProfileNutritionSettingsPage(ProfileNutritionSettingsViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Loads the settings when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileNutritionSettingsViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm && vm.HasUnsavedChanges)
        {
            vm.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>Commits the edited calorie target when the field is left.</summary>
    private void OnCaloriesCommitted(object? sender, EventArgs e)
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm)
            vm.CommitCaloriesEdit();
    }

    /// <summary>Commits the edited macro target when the field is left.</summary>
    private void OnMacroCommitted(object? sender, EventArgs e)
    {
        if (BindingContext is ProfileNutritionSettingsViewModel vm)
            vm.CommitMacroEdit();
    }
}
