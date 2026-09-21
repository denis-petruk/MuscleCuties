using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class InjuryLogPage : ContentPage
{
    public InjuryLogPage(InjuryLogViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((InjuryLogViewModel)BindingContext).LoadCommand.ExecuteAsync(null));
    }
}
