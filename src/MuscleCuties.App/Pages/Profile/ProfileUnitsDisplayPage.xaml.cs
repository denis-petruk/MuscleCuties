using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileUnitsDisplayPage : ContentPage
{
    public ProfileUnitsDisplayPage(ProfileUnitsDisplayViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileUnitsDisplayViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
