using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfileHealthSyncPage : ContentPage
{
    public ProfileHealthSyncPage(ProfileHealthSyncViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileHealthSyncViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
