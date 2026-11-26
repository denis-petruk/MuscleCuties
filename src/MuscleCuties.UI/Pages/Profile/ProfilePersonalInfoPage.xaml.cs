using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Personal details: name, birth date, and body measurements.
/// </summary>
public partial class ProfilePersonalInfoPage : ContentPage
{
    /// <summary>Initializes the page and its view model.</summary>
    public ProfilePersonalInfoPage(ProfilePersonalInfoViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Loads the profile when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfilePersonalInfoViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Releases the page when navigated away from.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        var vm = (ProfilePersonalInfoViewModel)BindingContext;
        vm.ResetMeasurementVisit();
        vm.MaskSensitiveInputs();
        base.OnNavigatedFrom(args);
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ProfilePersonalInfoViewModel vm && vm.HasUnsavedChanges)
        {
            vm.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>Opens the editor for the measurement row that was tapped and scrolls it into view.</summary>
    private async void OnEditMeasurementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: MeasurementHistoryRow reading } &&
            BindingContext is ProfilePersonalInfoViewModel viewModel &&
            viewModel.BeginEditMeasurement(reading))
        {
            await PersonalInfoScroll.ScrollToAsync(MeasurementEditor, ScrollToPosition.Start, false);
        }
    }

    /// <summary>Reports the new birth date so the age rule is re-applied.</summary>
    private void OnPersonalInfoBirthDateSelected(object? sender, DateChangedEventArgs e)
    {
        ((ProfilePersonalInfoViewModel)BindingContext).MarkBirthDateSelected();
    }
}
