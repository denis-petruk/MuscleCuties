using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Workout preferences: activity types, equipment, and training days.
/// </summary>
public partial class ProfileWorkoutPreferencesPage : ContentPage
{
    private readonly ProfileWorkoutPreferencesViewModel _viewModel;
    private readonly AsyncRelayCommand _backCommand;

    /// <summary>The command that returns to the profile.</summary>
    public AsyncRelayCommand BackCommand => _backCommand;

    /// <summary>Takes its view model and creates the back command the page's own back button runs.</summary>
    public ProfileWorkoutPreferencesPage(ProfileWorkoutPreferencesViewModel vm)
    {
        _viewModel = vm;
        _backCommand = new AsyncRelayCommand(HandleBackAsync);
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Loads the preferences when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(async () =>
        {
            var vm = (ProfileWorkoutPreferencesViewModel)BindingContext;
            await vm.LoadDataCommand.ExecuteAsync(null);
        });
        this.BeginDeferredLoad(() => PreferenceChoicesLazy.LoadIfNeededAsync(true).AsTask());
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsBusy)
            return true;

        _backCommand.Execute(null);
        return true;
    }

    /// <summary>Leaves the page once the view model is idle, so an in-flight save is not abandoned.</summary>
    private async Task HandleBackAsync()
    {
        if (_viewModel.IsBusy)
            return;

        if (_viewModel.HasUnsavedChanges)
        {
            var discardChanges = await DisplayAlertAsync(
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChangesTitle"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChangesMessage"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChanges"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_KeepEditing"));
            if (!discardChanges)
                return;
        }

        await _viewModel.BackCommand.ExecuteAsync(null);
    }
}
