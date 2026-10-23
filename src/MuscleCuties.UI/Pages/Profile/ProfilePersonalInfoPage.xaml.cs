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

    private void OnBirthDateSelected(object? sender, DateChangedEventArgs e)
    {
        ((ProfilePersonalInfoViewModel)BindingContext).MarkBirthDateSelected();
    }
}
