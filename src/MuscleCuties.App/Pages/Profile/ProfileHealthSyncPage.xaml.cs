using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileHealthSyncPage : ContentPage
{
    public ProfileHealthSyncPage(ProfileHealthSyncViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileHealthSyncViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
