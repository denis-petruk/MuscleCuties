using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
