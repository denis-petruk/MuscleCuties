using MuscleCuties.Core.ViewModels.Auth;

namespace MuscleCuties.App.Pages.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
