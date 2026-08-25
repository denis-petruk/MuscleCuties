using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePersonalInfoPage : ContentPage
{
    public ProfilePersonalInfoPage(ProfilePersonalInfoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfilePersonalInfoViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
