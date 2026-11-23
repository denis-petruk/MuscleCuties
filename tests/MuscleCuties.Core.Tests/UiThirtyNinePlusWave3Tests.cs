using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class UiThirtyNinePlusWave3Tests
{
    [Fact]
    public void WorkoutPage_never_traps_session_overlay_handles_toggle_errors_and_fixes_preview_grid()
    {
        var pageCs = UiSourceFixture.ReadUi("Pages", "Workout", "WorkoutPage.xaml.cs");
        var page = UiSourceFixture.ReadUi("Pages", "Workout", "WorkoutPage.xaml");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Workout", "WorkoutViewModel.cs");
        var featured = UiSourceFixture.ReadUi("Controls", "Workout", "FeaturedWorkoutCard.xaml");

        // P0: SessionActivitiesOverlay must never be disabled during load (keeps close/backdrop usable).
        Assert.DoesNotContain("SessionActivitiesOverlay.IsEnabled = false;", pageCs, StringComparison.Ordinal);
        // Back button closes Reschedule modal too.
        Assert.Contains("IsRescheduleModalVisible", pageCs, StringComparison.Ordinal);
        // Reschedule modal shows busy indicator.
        Assert.Contains("IsRescheduleBusy", page, StringComparison.Ordinal);
        // Toggle errors surface status instead of throwing unhandled exceptions.
        Assert.Contains("StatusMessage", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("HasScheduleDays =>", vm, StringComparison.Ordinal);
        // FeaturedWorkoutCard exercise preview uses VerticalStackLayout instead of RowDefinitions-less Grid.
        Assert.DoesNotContain("<Grid BindableLayout.ItemsSource=\"{Binding ExercisePreviews", featured, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutSessionPage_scopes_retry_to_initial_load_fixes_voiceover_and_adds_rir_help()
    {
        var page = UiSourceFixture.ReadUi("Pages", "Workout", "WorkoutSessionPage.xaml");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var swap = UiSourceFixture.ReadUi("Controls", "Workout", "WorkoutSwapPanel.xaml");

        // P0: Retry button only appears on initial load error, never wiping unsaved sets on save errors.
        Assert.Contains("_isInitialLoadError", vm, StringComparison.Ordinal);
        Assert.Contains("CanRetryLoad => HasError && _isInitialLoadError", vm, StringComparison.Ordinal);
        // BackAsync dismisses open modals before popping/suspending.
        Assert.Contains("IsMovementNoteOpen", vm, StringComparison.Ordinal);
        Assert.Contains("IsPainReportVisible", vm, StringComparison.Ordinal);
        // VoiceOver: Error & Celebration labels announce via behavior; RIR help hint & clean form label exist.
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("Workout_Session_RirHelpHint", page, StringComparison.Ordinal);
        Assert.Contains("AdaptiveRow", page, StringComparison.Ordinal);
        // Swap panel exposes prerequisite hint & retry on error.
        Assert.Contains("CanConfirmSwap", swap, StringComparison.Ordinal);
        Assert.Contains("Common_TryAgain", swap, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutHistoryPage_retries_initial_load_errors_validates_rpe_and_exposes_show_all_months()
    {
        var page = UiSourceFixture.ReadUi("Pages", "Workout", "WorkoutHistoryPage.xaml");
        var pageCs = UiSourceFixture.ReadUi("Pages", "Workout", "WorkoutHistoryPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Workout", "WorkoutHistoryViewModel.cs");

        // P0: Unified RetryCommand works when Sessions.Count == 0 (unlike LoadNextPageCommand).
        Assert.Contains("RetryCommand", page, StringComparison.Ordinal);
        Assert.Contains("ShowAllMonthsCommand", page, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("EditingRpeText", page, StringComparison.Ordinal);
        Assert.Contains("EditingRpeText", vm, StringComparison.Ordinal);
        Assert.Contains("OnBackButtonPressed", pageCs, StringComparison.Ordinal);
    }

    [Fact]
    public void StrengthPlanGenerationPage_notifies_prerequisite_recovery_and_reports_all_gaps_together()
    {
        var page = UiSourceFixture.ReadUi("Pages", "Workout", "StrengthPlanGenerationPage.xaml");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Workout", "StrengthPlanGenerationViewModel.cs");

        // P0 safety invariant: NeedsPrerequisiteRecovery MUST notify PropertyChanged and gaps must be collected together.
        Assert.Contains("nameof(NeedsPrerequisiteRecovery)", vm, StringComparison.Ordinal);
        Assert.Contains("CollectMissingPrerequisites", vm, StringComparison.Ordinal);
        // Preview sheet uses ModalSheetHeader with opaque footer; no dead CanEditDetailsFromPrerequisites inside preview.
        Assert.Contains("sharedControls:ModalSheetHeader", page, StringComparison.Ordinal);
        var previewStart = page.IndexOf("IsOpen=\"{Binding HasPreview}\"", StringComparison.Ordinal);
        Assert.True(previewStart > 0);
        Assert.DoesNotContain("CanEditDetailsFromPrerequisites", page[previewStart..], StringComparison.Ordinal);
        Assert.DoesNotContain(" WidthRequest=\"{StaticResource UiDimension52}\"", page, StringComparison.Ordinal);
        Assert.Contains("MinimumWidthRequest=\"{StaticResource UiDimension52}\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Nutrition_fixes_double_macro_prefix_inverted_log_hint_suggestion_dead_end_and_balance_a11y()
    {
        var vm = UiSourceFixture.ReadUi("ViewModels", "Nutrition", "NutritionViewModel.cs");
        var editor = UiSourceFixture.ReadUi("Controls", "Nutrition", "MealEditorModal.xaml");
        var suggestion = UiSourceFixture.ReadUi("Controls", "Nutrition", "MealSuggestionModal.xaml");
        var breakdown = UiSourceFixture.ReadUi("Controls", "Nutrition", "NutritionBreakdownModal.xaml");
        var balance = UiSourceFixture.ReadUi("Controls", "Nutrition", "NutritionBalanceCard.xaml");
        var page = UiSourceFixture.ReadUi("Pages", "Nutrition", "NutritionPage.xaml");
        var pageCs = UiSourceFixture.ReadUi("Pages", "Nutrition", "NutritionPage.xaml.cs");

        // P1: No double "P P 25 g" / "Б P 25 г" macro prefix.
        Assert.DoesNotContain("$\"P {DisplayFormat.Grams", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"C {DisplayFormat.Grams", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"F {DisplayFormat.Grams", vm, StringComparison.Ordinal);
        // LogMealHint shown cleanly in footer, not inverted on EmptyView.
        Assert.DoesNotContain("ShowLogMealHint, Converter={StaticResource InvertedBoolConverter}", editor, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", editor, StringComparison.Ordinal);
        // Suggestion modal empty state offers Refresh/Try again and uses SelectionCell.
        Assert.Contains("RefreshSuggestionsCommand", suggestion, StringComparison.Ordinal);
        Assert.Contains("sharedControls:SelectionCell", suggestion, StringComparison.Ordinal);
        // Breakdown modal announces errors and offers Retry.
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", breakdown, StringComparison.Ordinal);
        // Balance card announces RemainingCaloriesText & Level2 heading, and ViewModel notifies it on load.
        var notifications = UiSourceFixture.ReadUi("ViewModels", "Nutrition", "NutritionViewModel.Notifications.cs");
        Assert.Contains("RemainingCaloriesText", balance, StringComparison.Ordinal);
        Assert.Contains("nameof(RemainingCaloriesText)", notifications, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.HeadingLevel=\"Level2\"", balance, StringComparison.Ordinal);
        // NutritionPage handles all modals on back & uses AdaptiveRow for CTA row.
        Assert.Contains("IsMealDetailVisible", pageCs, StringComparison.Ordinal);
        Assert.Contains("IsSuggestionModalVisible", pageCs, StringComparison.Ordinal);
        Assert.Contains("AdaptiveRow Columns=\"2\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FoodLogPage_exposes_add_food_entry_confirms_deletes_and_fixes_hydration_voiceover()
    {
        var page = UiSourceFixture.ReadUi("Pages", "Nutrition", "FoodLogPage.xaml");
        var pageCs = UiSourceFixture.ReadUi("Pages", "Nutrition", "FoodLogPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Nutrition", "FoodLogViewModel.cs");
        var hydration = UiSourceFixture.ReadUi("Controls", "Nutrition", "HydrationLogView.xaml");
        var quickRow = UiSourceFixture.ReadUi("Controls", "Nutrition", "QuickFoodItemRow.xaml");

        // P0: OpenAddFoodCommand is reachable on FoodLogPage.
        Assert.Contains("OpenAddFoodCommand", page, StringComparison.Ordinal);
        // Search Entry executes SearchFoodCommand on keyboard Search.
        Assert.Contains("ReturnCommand=\"{Binding SearchFoodCommand}\"", page, StringComparison.Ordinal);
        // Status toast announces via ScreenReaderStatusAnnouncementBehavior; back button closes edit modal.
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", page, StringComparison.Ordinal);
        Assert.Contains("OnBackButtonPressed", pageCs, StringComparison.Ordinal);
        // Grouped meal draft macros have localized P/C/F prefixes.
        Assert.Contains("Nutrition_Macro_ProteinShort", page, StringComparison.Ordinal);
        // DeleteFoodItemAsync confirms before deleting.
        var deleteIdx = vm.IndexOf("DeleteFoodItemAsync", StringComparison.Ordinal);
        Assert.True(deleteIdx > 0);
        Assert.Contains("ConfirmAsync", vm[deleteIdx..Math.Min(vm.Length, deleteIdx + 900)], StringComparison.Ordinal);
        // Hydration card announces actual ml total to VoiceOver & uses ScreenReaderStatusAnnouncementBehavior.
        Assert.Contains("WaterSummaryAccessibility", hydration, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", hydration, StringComparison.Ordinal);
        // QuickFoodItemRow separates title row from action buttons so buttons never crush food name.
        Assert.DoesNotContain("ColumnDefinitions=\"Auto,*,Auto,Auto\"", quickRow, StringComparison.Ordinal);
    }
}
