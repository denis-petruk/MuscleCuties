using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileUnitsDisplayPage : ContentPage
{
    public ProfileUnitsDisplayPage(ProfileUnitsDisplayViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileUnitsDisplayViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
