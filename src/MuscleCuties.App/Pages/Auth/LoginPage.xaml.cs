using MuscleCuties.Core.ViewModels.Auth;

namespace MuscleCuties.App.Pages.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
