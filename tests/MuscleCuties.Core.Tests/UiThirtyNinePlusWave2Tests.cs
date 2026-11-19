using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class UiThirtyNinePlusWave2Tests
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
    public void PageLoadingOverlay_respects_explicit_null_RetryCommand_for_non_retryable_errors()
    {
        var cs = ReadUi("Controls", "Shared", "PageLoadingOverlay.xaml.cs");
        Assert.Contains("IsSet(RetryCommandProperty)", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_fixes_imperial_weight_bug_voiceover_nutrition_contrast_and_deferred_order()
    {
        var page = ReadUi("Pages", "Dashboard", "DashboardPage.xaml");
        var pageCs = ReadUi("Pages", "Dashboard", "DashboardPage.xaml.cs");
        var trendsVm = ReadUi("ViewModels", "Dashboard", "DashboardViewModel.Trends.cs");
        var nutritionCard = ReadUi("Controls", "Dashboard", "DashboardNutritionCard.xaml");
        var readinessCard = ReadUi("Controls", "Dashboard", "ReadinessRecoveryCard.xaml");

        // Deferred card load order matches visual reading order: Workout before Phase.
        var workoutIdx = pageCs.IndexOf("WorkoutCardLazy.LoadIfNeededAsync", StringComparison.Ordinal);
        var phaseIdx = pageCs.IndexOf("PhaseCardLazy.LoadIfNeededAsync", StringComparison.Ordinal);
        Assert.True(workoutIdx > 0 && phaseIdx > workoutIdx,
            "DashboardPage deferred card load order must match visual order (Workout before Phase).");

        // Streaks uses AdaptiveRow instead of rigid 3-star grid; Photo row uses Auto,* instead of 40,*,Auto.
        Assert.Contains("<sharedControls:AdaptiveRow Columns=\"3\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"40,*,Auto\"", page, StringComparison.Ordinal);
        Assert.Contains("InjuryButtonAccessibilityText", page, StringComparison.Ordinal);

        // Imperial weekly weight summary converts kg via UnitsFormatter.ToDisplayWeight and localizes minutes.
        Assert.DoesNotContain("WeeklyWeightPoints[^1].WeightKg?.ToString", trendsVm, StringComparison.Ordinal);
        Assert.Contains("DisplayFormat.Minutes", trendsVm, StringComparison.Ordinal);

        // Nutrition card exposes dynamic accessible summary on CardTapTarget and avoids literal 76,*.
        Assert.Contains("NutritionCardAccessibilityText", nutritionCard, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"76,*\"", nutritionCard, StringComparison.Ordinal);

        // Readiness card fixes WCAG AA contrast, adds ring descriptions, and lets completed check-in be reviewed.
        Assert.DoesNotContain("Light={StaticResource Primary}", readinessCard, StringComparison.Ordinal);
        Assert.Contains("Dashboard_Readiness_ReadinessScoreFormat", readinessCard, StringComparison.Ordinal);
        Assert.Contains("Dashboard_Readiness_RecoveryScoreFormat", readinessCard, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyCheckIn_hides_form_when_completed_keeps_scale_anchors_and_announces_errors()
    {
        var content = ReadUi("Controls", "Dashboard", "DailyCheckInContent.xaml");
        var modal = ReadUi("Controls", "Dashboard", "DailyCheckInModal.xaml");
        var cs = ReadUi("Controls", "Dashboard", "DailyCheckInContent.xaml.cs");

        Assert.Contains("IsCompleted, Converter={StaticResource InvertedBoolConverter}", content, StringComparison.Ordinal);
        Assert.DoesNotContain("HideAtAccessibilitySize=\"True\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("TextColor=\"{StaticResource Primary}\"", content, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", content, StringComparison.Ordinal);
        Assert.Contains("Common_Selected", cs, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.ColumnSpan=\"2\"", modal, StringComparison.Ordinal);
    }

    [Fact]
    public void Cycle_fixes_calendar_weekday_headers_dark_warning_contrast_and_load_error_recovery()
    {
        var grid = ReadUi("Controls", "Cycle", "CycleMonthGrid.cs");
        var warning = ReadUi("Controls", "Cycle", "CycleWarningPopup.xaml");
        var page = ReadUi("Pages", "Cycle", "CyclePage.xaml");
        var vm = ReadUi("ViewModels", "Cycle", "CycleViewModel.cs");
        var quickLog = ReadUi("Controls", "Cycle", "CycleQuickLogBottomSheet.xaml");
        var summary = ReadUi("Controls", "Cycle", "CyclePhaseSummarySheet.xaml");

        // Month grid renders weekday headers + DayOfWeek leading offset + selected VoiceOver hint.
        Assert.Contains("DayOfWeek", grid, StringComparison.Ordinal);
        Assert.Contains("Common_Selected", grid, StringComparison.Ordinal);

        // Warning popup uses high-contrast WarningSurface/WarningAccent in both themes.
        Assert.Contains("WarningSurfaceDark", warning, StringComparison.Ordinal);
        Assert.Contains("WarningAccentDark", warning, StringComparison.Ordinal);

        // CyclePage has RefreshView, PageHeader Title=Cycle_Page_Title, and classified PageLoadingOverlay.
        Assert.Contains("<RefreshView", page, StringComparison.Ordinal);
        Assert.Contains("Title=\"{loc:LocalizedText Cycle_Page_Title}\"", page, StringComparison.Ordinal);
        Assert.Contains("MessageText=\"{Binding LoadErrorMessage}\"", page, StringComparison.Ordinal);
        Assert.Contains("ApiFailurePresenter.FromException", vm, StringComparison.Ordinal);

        // QuickLog sheet shows StateBusyInline + ScreenReaderStatusAnnouncementBehavior + non-fixed symptom basis.
        Assert.Contains("StateBusyInline", quickLog, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", quickLog, StringComparison.Ordinal);
        Assert.DoesNotContain("FlexLayout.Basis=\"48%\"", quickLog, StringComparison.Ordinal);

        // Phase summary sheet headers use Auto,* so Russian wraps cleanly.
        Assert.DoesNotContain("ColumnDefinitions=\"Auto,Auto\"", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void CyclePhaseDetailPage_renders_point_body_text_and_expands_safety_warnings_by_default()
    {
        var page = ReadUi("Pages", "Cycle", "CyclePhaseDetailPage.xaml");
        var vm = ReadUi("ViewModels", "CyclePhaseDetailViewModel.cs");

        // Point.Text is rendered in Workouts, Why, Watch, and Nutrition (4 occurrences).
        Assert.True(Regex.Matches(page, @"Binding Text,\s*Converter=\{StaticResource LocalizedStringConverter\}").Count >= 4,
            "CyclePhaseDetailPage must render Point.Text in Workouts, Why, Watch, and Nutrition sections.");
        Assert.DoesNotContain("Style=\"{StaticResource PillBadge}\"", page, StringComparison.Ordinal);
        Assert.Contains("Cycle_Page_MedicalDisclaimer", page, StringComparison.Ordinal);
        Assert.Contains("IsWatchExpanded = detail.WatchPoints.Count > 0;", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void InjuryLogModal_hugs_empty_content_and_exposes_plan_refresh_retry_and_save_hint()
    {
        var modal = ReadUi("Controls", "Injury", "InjuryLogModal.xaml");
        var content = ReadUi("Controls", "Injury", "InjuryLogContent.xaml");

        Assert.DoesNotContain("MinimumHeightRequest=\"{StaticResource ModalSheetMaximumHeight}\"", modal, StringComparison.Ordinal);
        Assert.Contains("SaveInjuryPrerequisiteHint", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("<CollectionView", content, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ModalSheetBody}\"", content, StringComparison.Ordinal);
        Assert.Contains("Injury_LogContent_RefreshPlan", content, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", content, StringComparison.Ordinal);
    }
}
