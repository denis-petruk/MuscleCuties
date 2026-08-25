using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileFeedbackPage : ContentPage
{
    public ProfileFeedbackPage(ProfileFeedbackViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileFeedbackViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
