using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePersonalInfoPage : ContentPage
{
    public ProfilePersonalInfoPage(ProfilePersonalInfoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfilePersonalInfoViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
