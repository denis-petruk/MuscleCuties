using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class ProfileSetupPage : ContentPage
{
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
