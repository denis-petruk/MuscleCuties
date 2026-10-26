using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

public partial class ProfileWorkoutPreferencesPage : ContentPage
{
    private readonly ProfileWorkoutPreferencesViewModel _viewModel;
    private readonly AsyncRelayCommand _backCommand;

    public AsyncRelayCommand BackCommand => _backCommand;

    public ProfileWorkoutPreferencesPage(ProfileWorkoutPreferencesViewModel vm)
    {
        _viewModel = vm;
        _backCommand = new AsyncRelayCommand(HandleBackAsync);
        this.InitializeAndBind(InitializeComponent, vm);
    }

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

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsBusy)
            return true;

        _backCommand.Execute(null);
        return true;
    }

    private async Task HandleBackAsync()
    {
        if (_viewModel.IsBusy)
            return;

        if (_viewModel.HasUnsavedChanges)
        {
            var discardChanges = await DisplayAlertAsync(
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChangesTitle"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChangesMessage"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_KeepEditing"),
                UiTextCatalog.Get("Profile_WorkoutPrefs_DiscardChanges"));
            if (!discardChanges)
                return;
        }

        await _viewModel.BackCommand.ExecuteAsync(null);
    }
}
