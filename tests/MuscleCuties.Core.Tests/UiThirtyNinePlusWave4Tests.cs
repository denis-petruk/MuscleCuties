using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class UiThirtyNinePlusWave4Tests
{
    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")) ||
                Directory.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }

    private static string ReadUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", Path.Combine(parts)));

    [Fact]
    public void ProfilePage_guards_logout_adds_card_tap_targets_and_upgrades_crop_modal()
    {
        var page = ReadUi("Pages", "Profile", "ProfilePage.xaml");
        var details = ReadUi("Controls", "Profile", "ProfileDetailsContent.xaml");
        var crop = ReadUi("Controls", "Profile", "ProfileImageCropModal.xaml");
        var cropCs = ReadUi("Controls", "Profile", "ProfileImageCropModal.xaml.cs");
        var vm = ReadUi("ViewModels", "Profile", "ProfileViewModel.cs");
        var ru = ReadUi("Services", "Localization", "RussianUiText.NutritionProfileAuth.cs");

        // Logout guarded by IsBusy + AccountActionError; ScreenReaderStatusAnnouncementBehavior on error.
        Assert.Contains("AccountActionError", vm, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        // Menu rows & avatar expose CardTapTarget; Crop modal uses ModalSheetHeader + _isSaving guard.
        Assert.Contains("sharedControls:CardTapTarget", details, StringComparison.Ordinal);
        Assert.Contains("ModalSheetHeader", crop, StringComparison.Ordinal);
        Assert.Contains("_isSaving", cropCs, StringComparison.Ordinal);
        // RU log streak copy uses natural Russian ("Серия питания").
        Assert.Contains("Серия питания", ru, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfilePersonalInfoPage_hides_tab_bar_uses_page_loading_overlay_and_guards_unsaved_back()
    {
        var page = ReadUi("Pages", "Profile", "ProfilePersonalInfoPage.xaml");
        var vm = ReadUi("ViewModels", "Profile", "ProfilePersonalInfoViewModel.cs");

        // P0: Tab bar hidden so pinned Save footer is never occluded.
        Assert.Contains("Shell.TabBarIsVisible=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("SafeAreaEdges=\"All\"", page, StringComparison.Ordinal);
        // IPageLoadAware + PageLoadingOverlay + ScreenReaderStatusAnnouncementBehavior + AdaptiveRow.
        Assert.Contains("IPageLoadAware", vm, StringComparison.Ordinal);
        Assert.Contains("PageLoadingOverlay", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("AdaptiveRow", page, StringComparison.Ordinal);
        Assert.Contains("HasUnsavedChanges", vm, StringComparison.Ordinal);
        Assert.Contains("IsSpellCheckEnabled=\"False\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileWorkoutPreferencesPage_hides_tab_bar_collects_all_gaps_and_uses_cards()
    {
        var page = ReadUi("Pages", "Profile", "ProfileWorkoutPreferencesPage.xaml");
        var choices = ReadUi("Controls", "Profile", "ProfileWorkoutPreferencesChoices.xaml");
        var vm = ReadUi("ViewModels", "Profile", "ProfileWorkoutPreferencesViewModel.cs");

        // P0: Tab bar hidden so pinned Save footer is never occluded.
        Assert.Contains("Shell.TabBarIsVisible=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("SafeAreaEdges=\"All\"", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("Profile_WorkoutPrefs_ExerciseExclusionsHint", choices, StringComparison.Ordinal);
        Assert.Contains("ClearButtonVisibility=\"WhileEditing\"", choices, StringComparison.Ordinal);
        // All validation gaps reported together.
        Assert.Contains("CollectValidationErrors", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileNutritionSettingsPage_hides_tab_bar_validates_bounds_and_stops_keystroke_macro_wipe()
    {
        var page = ReadUi("Pages", "Profile", "ProfileNutritionSettingsPage.xaml");
        var vm = ReadUi("ViewModels", "Profile", "ProfileNutritionSettingsViewModel.cs");

        // P0: Tab bar hidden + PageLoadingOverlay + ScreenReaderStatusAnnouncementBehavior.
        Assert.Contains("Shell.TabBarIsVisible=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("SafeAreaEdges=\"All\"", page, StringComparison.Ordinal);
        Assert.Contains("IPageLoadAware", vm, StringComparison.Ordinal);
        Assert.Contains("PageLoadingOverlay", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("ResetToCalculatedCommand", page, StringComparison.Ordinal);
        Assert.Contains("HasUnsavedChanges", vm, StringComparison.Ordinal);
        Assert.Contains("CollectValidationErrors", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileUnitsDisplayPage_hides_tab_bar_notifies_profile_refresh_and_guards_save()
    {
        var page = ReadUi("Pages", "Profile", "ProfileUnitsDisplayPage.xaml");
        var vm = ReadUi("ViewModels", "Profile", "ProfileUnitsDisplayViewModel.cs");

        // P0: Tab bar hidden + IProfileRefreshNotifier invalidates Today/Profile on save.
        Assert.Contains("Shell.TabBarIsVisible=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("SafeAreaEdges=\"All\"", page, StringComparison.Ordinal);
        Assert.Contains("IProfileRefreshNotifier", vm, StringComparison.Ordinal);
        Assert.Contains("IPageLoadAware", vm, StringComparison.Ordinal);
        Assert.Contains("PageLoadingOverlay", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("SelectionRadioRing", page, StringComparison.Ordinal);
        Assert.Contains("HasUnsavedChanges", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfilePrivacyPage_confirms_health_disconnect_fixes_async_void_back_and_announces_status()
    {
        var page = ReadUi("Pages", "Profile", "ProfilePrivacyPage.xaml");
        var pageCs = ReadUi("Pages", "Profile", "ProfilePrivacyPage.xaml.cs");
        var vm = ReadUi("ViewModels", "Profile", "ProfileHealthSyncViewModel.cs");

        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("StateBusyInline", page, StringComparison.Ordinal);
        Assert.DoesNotContain("new Command(async () =>", pageCs, StringComparison.Ordinal);
        Assert.Contains("AsyncRelayCommand", pageCs, StringComparison.Ordinal);
        Assert.Contains("DisplayAlertAsync", vm, StringComparison.Ordinal);
        Assert.Contains("NotifyCanExecuteChangedFor", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileFeedbackPage_validates_contact_email_animates_expanders_and_adds_copy_fallback()
    {
        var page = ReadUi("Pages", "Profile", "ProfileFeedbackPage.xaml");
        var vm = ReadUi("ViewModels", "Profile", "ProfileFeedbackViewModel.cs");

        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("StateBusyInline", page, StringComparison.Ordinal);
        Assert.Contains("IsSpellCheckEnabled=\"False\"", page, StringComparison.Ordinal);
        Assert.Contains("CopyFeedbackCommand", page, StringComparison.Ordinal);
        Assert.Contains("HasUnsavedDraft", vm, StringComparison.Ordinal);
        Assert.Contains("Rotation\" Value=\"180\"", page, StringComparison.Ordinal);
        Assert.Contains("Profile_Feedback_SubmitHint", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PhotoJourneyVaultPage_confirms_delete_extracts_comparison_modal_and_fixes_privacy_reveal()
    {
        var page = ReadUi("Pages", "Profile", "PhotoJourneyVaultPage.xaml");
        var vm = ReadUi("ViewModels", "Profile", "PhotoJourneyVaultViewModel.cs");
        var comparisonPath = Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", "Controls", "Profile", "PhotoComparisonModal.xaml");

        Assert.True(File.Exists(comparisonPath), "PhotoComparisonModal.xaml should be extracted as an AnimatedModalView.");
        var comparison = File.ReadAllText(comparisonPath);
        Assert.Contains("AnimatedModalView", comparison, StringComparison.Ordinal);
        Assert.Contains("ModalSheetHeader", comparison, StringComparison.Ordinal);

        Assert.Contains("DisplayAlertAsync", vm, StringComparison.Ordinal);
        Assert.Contains("CanComparePhotos", vm, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("BackCommand=\"{Binding NavigateBackCommand}\"", page, StringComparison.Ordinal);
        Assert.Contains("MinimumHeightRequest=\"{StaticResource UiDimension240}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("UiDimension640", page, StringComparison.Ordinal);
    }
}
