using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfileUnitsDisplayPage : ContentPage
{
    public ProfileUnitsDisplayPage(ProfileUnitsDisplayViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileUnitsDisplayViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
