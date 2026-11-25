using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Units and display preferences.
/// </summary>
public partial class ProfileUnitsDisplayPage : ContentPage
{
    /// <summary>Initializes the page and its view model.</summary>
    public ProfileUnitsDisplayPage(ProfileUnitsDisplayViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Loads the unit preference when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileUnitsDisplayViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ProfileUnitsDisplayViewModel vm && vm.HasUnsavedChanges)
        {
            vm.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
