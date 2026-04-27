using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class ProfileSetupPage : ContentPage
{
    public ProfileSetupPage(ProfileSetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
