using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ProfileViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }
}
