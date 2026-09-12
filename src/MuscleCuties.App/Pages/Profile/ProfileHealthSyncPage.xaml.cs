using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileHealthSyncPage : ContentPage
{
    public ProfileHealthSyncPage(ProfileHealthSyncViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileHealthSyncViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
