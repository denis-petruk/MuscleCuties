using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages.Profile;

public partial class InjuryLogPage : ContentPage
{
    public InjuryLogPage(InjuryLogViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((InjuryLogViewModel)BindingContext).LoadCommand.ExecuteAsync(null));
    }
}
