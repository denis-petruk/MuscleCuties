using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfilePersonalInfoPage : ContentPage
{
    public ProfilePersonalInfoPage(ProfilePersonalInfoViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfilePersonalInfoViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        ((ProfilePersonalInfoViewModel)BindingContext).ResetMeasurementVisit();
        base.OnNavigatedFrom(args);
    }

    private async void OnEditMeasurementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: MeasurementHistoryRow reading } &&
            BindingContext is ProfilePersonalInfoViewModel viewModel &&
            viewModel.BeginEditMeasurement(reading))
        {
            await PersonalInfoScroll.ScrollToAsync(MeasurementEditor, ScrollToPosition.Start, false);
        }
    }

    private void OnBirthDateSelected(object? sender, DateChangedEventArgs e)
    {
        ((ProfilePersonalInfoViewModel)BindingContext).MarkBirthDateSelected();
    }
}
