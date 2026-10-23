using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfileWorkoutPreferencesPage : ContentPage
{
    public ProfileWorkoutPreferencesPage(ProfileWorkoutPreferencesViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(async () =>
        {
            var vm = (ProfileWorkoutPreferencesViewModel)BindingContext;
            await vm.LoadDataCommand.ExecuteAsync(null);
            WorkoutFrequencySlider.Value = vm.DaysPerWeek;
        });
        this.BeginDeferredLoad(() => PreferenceChoicesLazy.LoadIfNeededAsync(true).AsTask());
    }
}
