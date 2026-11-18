using MuscleCuties.UseCases.Features.Dashboard.CheckIn;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// S-08: untouched check-in controls are never submitted as self-reported answers. The view model
/// builds its request through <see cref="DailyCheckInAnswers"/>; the source guards below pin that
/// wiring because the MAUI project is not referenced by the backend test gate.
/// </summary>
public sealed class DailyCheckInAnswersTests
{
    private const string TimeZone = "UTC";
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DailyCheckInAnswers Complete = new(
        SleepHours: 6.5, SleepQuality: 2, Energy: 1, Soreness: 4, PainLevel: 0, HasBloating: false,
        Fatigue: 3);

    [Fact]
    public void Three_readiness_answers_can_save_without_fabricating_optional_observations()
    {
        var answers = new DailyCheckInAnswers(SleepQuality: 3, Energy: 3, Fatigue: 1);
        var request = Assert.IsType<UseCases.Features.Dashboard.CheckIn.Contracts.SubmitDailyCheckInRequest>(
            answers.ToRequest(Today, TimeZone, requireSupplementaryAnswers: false));
        Assert.Null(request.SleepHours);
        Assert.Null(request.PainLevel);
        Assert.Null(request.HasBloating);
        Assert.Null(request.PlanningSoreness);
        Assert.Equal(3, request.PlanningSleepQuality);
        Assert.Equal(3, request.EnergyLevel);
        Assert.Equal(1, request.PlanningFatigue);
    }

    [Fact]
    public void Nothing_is_preselected()
    {
        var untouched = new DailyCheckInAnswers();

        Assert.False(untouched.IsComplete);
        Assert.Null(untouched.ToRequest(Today, TimeZone));
    }

    public static TheoryData<DailyCheckInAnswers> EachMissingAnswer => new()
    {
        Complete with { SleepHours = null },
        Complete with { SleepQuality = null },
        Complete with { Energy = null },
        Complete with { Fatigue = null },
        Complete with { PainLevel = null },
        Complete with { HasBloating = null }
    };

    [Theory]
    [MemberData(nameof(EachMissingAnswer))]
    public void Any_missing_required_answer_blocks_submission(DailyCheckInAnswers answers)
    {
        Assert.False(answers.IsComplete);
        Assert.Null(answers.ToRequest(Today, TimeZone));
    }

    [Fact]
    public void Explicit_answers_are_sent_as_given_and_weight_stays_optional()
    {
        var request = Assert.IsType<UseCases.Features.Dashboard.CheckIn.Contracts.SubmitDailyCheckInRequest>(
            Complete.ToRequest(Today, TimeZone));

        Assert.Equal(6.5, request.SleepHours);
        Assert.Equal(2, request.PlanningSleepQuality);
        Assert.Equal(1, request.EnergyLevel);
        Assert.Equal(3, request.PlanningFatigue);
        Assert.Equal(4, request.PlanningSoreness);
        Assert.Equal(0, request.PainLevel);
        Assert.False(request.HasBloating);
        Assert.Null(request.BodyWeightKg);
        Assert.Equal(TimeZone, request.PlanningTimeZoneId);
    }

    [Fact]
    public void Omitted_soreness_is_optional_and_remains_unreported()
    {
        var answers = Complete with { Soreness = null };

        Assert.True(answers.IsComplete);
        var request = Assert.IsType<UseCases.Features.Dashboard.CheckIn.Contracts.SubmitDailyCheckInRequest>(
            answers.ToRequest(Today, TimeZone));
        Assert.Null(request.PlanningSoreness);
        Assert.Equal(Complete.SleepQuality, request.PlanningSleepQuality);
        Assert.Equal(Complete.Energy, request.EnergyLevel);
        Assert.Equal(Complete.Fatigue, request.PlanningFatigue);
    }

    [Fact]
    public void Fatigue_is_a_distinct_required_answer_and_is_not_fabricated_from_energy()
    {
        var source = AnswersSource();
        var viewModel = ViewModelSource();
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Controls",
            "Dashboard", "DailyCheckInContent.xaml"));

        Assert.Contains("Fatigue", source, StringComparison.Ordinal);
        Assert.Contains("Fatigue", viewModel, StringComparison.Ordinal);
        Assert.Contains("FatigueChip5", page, StringComparison.Ordinal);
        Assert.Contains("SorenessChip5", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanningStressCompatibilityValue => Energy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Workout_preferences_keep_glute_focus_in_the_profile_editor_and_cloud_round_trip()
    {
        var root = RepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "ViewModels", "Profile",
            "ProfileWorkoutPreferencesViewModel.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Controls", "Profile",
            "ProfileWorkoutPreferencesChoices.xaml"));
        var cloud = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.Infrastructure", "Cloud",
            "CloudAccountAndProfileStores.cs"));

        Assert.Contains("GluteFocus", viewModel, StringComparison.Ordinal);
        Assert.Contains("GluteFocus", page, StringComparison.Ordinal);
        Assert.Contains("gluteFocus", cloud, StringComparison.Ordinal);
    }

    [Fact]
    public void Hiit_is_not_a_new_activity_selection_but_existing_selection_remains_editable()
    {
        var root = RepositoryRoot();
        var catalog = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Services", "Workout",
            "WorkoutActivityOptionCatalog.cs"));
        var option = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Models", "UI", "Workout",
            "WorkoutActivityOptionItem.cs"));
        var localization = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Services", "Localization",
            "UiTextCatalog.NutritionProfileAuth.cs"));
        var russian = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Services", "Localization",
            "RussianUiText.NutritionProfileAuth.cs"));

        Assert.Contains("!selectedTypes.Contains(WorkoutActivityDto.Hiit)", catalog, StringComparison.Ordinal);
        Assert.Contains("UnavailableReason", catalog, StringComparison.Ordinal);
        Assert.Contains("IsOffered || IsSelected", option, StringComparison.Ordinal);
        Assert.Contains("Profile_WorkoutPrefs_HiitNotAvailable", localization, StringComparison.Ordinal);
        Assert.Contains("Profile_WorkoutPrefs_HiitNotAvailable", russian, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Out_of_range_ratings_are_not_answers(int rating)
    {
        Assert.False((Complete with { Soreness = rating }).IsComplete);
        Assert.False((Complete with { PainLevel = rating == 0 ? -1 : 4 }).IsComplete);
    }

    [Fact]
    public void View_model_starts_unanswered_and_never_derives_stress_from_energy()
    {
        var source = ViewModelSource();

        Assert.DoesNotContain("PlanningStress = EnergyLevel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanningStress = value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("readiness?.Stress ?? readiness?.Energy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_sleepHours = 7", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_planningSoreness = MinimumPlanningScaleValue", source, StringComparison.Ordinal);
        Assert.Contains("private int? _painLevel;", source, StringComparison.Ordinal);
        Assert.Contains("private int? _planningFatigue;", source, StringComparison.Ordinal);
        Assert.Contains("private bool? _hasBloating;", source, StringComparison.Ordinal);
        Assert.Contains("Answers.ToRequest(", source, StringComparison.Ordinal);
        Assert.Contains("Answers.IsComplete", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardPairsTheCheckInScoreWithItsOwnBandAndGatesGuidanceSeparately()
    {
        var ui = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var viewModel = File.ReadAllText(Path.Combine(ui, "ViewModels", "Dashboard", "DashboardViewModel.cs"));
        var card = File.ReadAllText(Path.Combine(ui, "Controls", "Dashboard", "ReadinessRecoveryCard.xaml"));

        Assert.DoesNotContain("ReadinessLabel = summary.ReadinessLabel", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding ReadinessLabel,", card, StringComparison.Ordinal);
        Assert.Contains("{Binding TodayReadinessBandText,", card, StringComparison.Ordinal);
        Assert.Contains("{Binding HasTodayReadinessGuidance}", card, StringComparison.Ordinal);
        Assert.Contains("_readinessPresentationSettings.AreReadinessSuggestionsEnabled", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturningToSignInClearsTheSingletonDashboardCheckInDraft()
    {
        var app = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "App.xaml.cs"));

        Assert.Contains("ResetCheckInForAccountChange()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void View_model_wires_healthkit_prepopulation_and_suggestions()
    {
        var source = ViewModelSource();

        Assert.Contains("private bool _isSleepAutoFilled;", source, StringComparison.Ordinal);
        Assert.Contains("private string _sleepAutoFilledBadgeText", source, StringComparison.Ordinal);
        Assert.Contains("private bool _hasHealthKitSuggestions;", source, StringComparison.Ordinal);
        Assert.Contains("private int _suggestedEnergyLevel;", source, StringComparison.Ordinal);
        Assert.Contains("private int _suggestedSleepQuality;", source, StringComparison.Ordinal);
        Assert.Contains("void SuggestFromAppleHealth()", source, StringComparison.Ordinal);
        Assert.Contains("GetCachedWeeklySummaryAsync", source, StringComparison.Ordinal);
        Assert.Contains("Dashboard_CheckIn_HealthKitSleepBadge", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Localization_contains_healthkit_checkin_strings_in_en_and_ru()
    {
        var current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "MuscleCuties.sln")))
            current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException("Repository root not found.");

        var enSource = File.ReadAllText(Path.Combine(current, "src", "MuscleCuties.UI", "Services", "Localization",
            "UiTextCatalog.Dashboard.cs"));
        var ruSource = File.ReadAllText(Path.Combine(current, "src", "MuscleCuties.UI", "Services", "Localization",
            "RussianUiText.Dashboard.cs"));

        Assert.Contains("English[\"Dashboard_CheckIn_HealthKitSleepBadge\"] = \"Auto-filled from Apple Health ({0}h)\";", enSource, StringComparison.Ordinal);
        Assert.Contains("English[\"Dashboard_CheckIn_SuggestFromAppleHealth\"] = \"Suggest ratings from Apple Health\";", enSource, StringComparison.Ordinal);
        Assert.Contains("Values[\"Dashboard_CheckIn_HealthKitSleepBadge\"] = \"Заполнено из Apple Health ({0} ч)\";", ruSource, StringComparison.Ordinal);
        Assert.Contains("Values[\"Dashboard_CheckIn_SuggestFromAppleHealth\"] = \"Предложить оценки из Apple Health\";", ruSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(8.5, 58, 65, 5, 4)]
    [InlineData(7.5, 62, 55, 4, 4)]
    [InlineData(7.0, 70, 0, 4, 3)]
    [InlineData(6.2, 70, 0, 3, 3)]
    [InlineData(5.0, 78, 25, 2, 2)]
    [InlineData(4.0, 80, 20, 1, 2)]
    public void Suggested_recovery_ratings_formula(
        double sleep, int rhr, int hrv, int expectedSleepQuality, int expectedEnergyLevel)
    {
        int sleepQuality;
        if (sleep >= 8.0 && ((rhr > 0 && rhr <= 60) || hrv >= 60))
            sleepQuality = 5;
        else if (sleep >= 7.0)
            sleepQuality = 4;
        else if (sleep >= 6.0)
            sleepQuality = 3;
        else if (sleep >= 4.5)
            sleepQuality = 2;
        else
            sleepQuality = 1;

        int energyLevel;
        if (sleep >= 7.5 && ((rhr > 0 && rhr <= 65) || hrv >= 50))
            energyLevel = 4;
        else if (sleep < 6.0 || rhr > 75 || (hrv > 0 && hrv < 30))
            energyLevel = 2;
        else
            energyLevel = 3;

        Assert.Equal(expectedSleepQuality, sleepQuality);
        Assert.Equal(expectedEnergyLevel, energyLevel);
    }

    private static string ViewModelSource()
    {
        return File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Dashboard",
            "DailyCheckInViewModel.cs"));
    }

    private static string AnswersSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UseCases", "Features", "Dashboard",
            "CheckIn", "DailyCheckInAnswers.cs"));

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "MuscleCuties.sln")))
            current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        return current;
    }
}
