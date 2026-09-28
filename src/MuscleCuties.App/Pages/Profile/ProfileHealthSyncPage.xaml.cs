using System.Diagnostics;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileHealthSyncPage : ContentPage
{
    public ProfileHealthSyncPage(ProfileHealthSyncViewModel vm)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        this.BindWithTiming(vm, started);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() =>
            ((ProfileHealthSyncViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }
}
