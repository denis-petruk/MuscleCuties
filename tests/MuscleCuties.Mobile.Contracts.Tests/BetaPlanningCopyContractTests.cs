using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
/// EN/RU parity for the beta planning copy (G-01, U-04/U-05, SessionWithheld, C1/OD-7, D1/OD-2). The
/// catalogs are read as source so the guard runs without the MAUI project.
/// </summary>
public sealed class BetaPlanningCopyContractTests
{
    private static readonly Regex Entry = new(@"\[""(?<key>[^""]+)""\]\s*=\s*""(?<value>(?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Theory]
    [InlineData("Workout_Generation_ApplyReplacesActivePlan")]
    [InlineData("Workout_Generation_ReplacesActivePlanNotice")]
    [InlineData("Workout_Generation_ApplyUnavailableNoStrengthSessions")]
    [InlineData("Workout_Generation_SessionWithheldOne")]
    [InlineData("Workout_Generation_SessionWithheldManyFormat")]
    [InlineData("Workout_Generation_StyleNeedsHistory")]
    [InlineData("Workout_Generation_SwitchToGeneralStrength")]
    [InlineData("Workout_Generation_SwitchStyleFailed")]
    [InlineData("Workout_Generation_EquipmentBetaNotice")]
    [InlineData("Workout_Generation_QuizEquipmentNotice")]
    [InlineData("Workout_Recommendation_BandStartingTension")]
    [InlineData("Workout_Generation_SelectionReason_BodyweightZeroAddedLoad")]
    [InlineData("Workout_Generation_SelectionReason_InitialLoadCalibrationRequired")]
    [InlineData("Workout_Generation_SelectionReason_PriorComparableHistory")]
    [InlineData("Workout_Generation_SelectionReason_ConservativeReturnLoadApplied")]
    [InlineData("Nutrition_Balance_ActivityFormat")]
    [InlineData("Workout_Recommendation_BandProgressReps")]
    [InlineData("Workout_Generation_MissingUpperPullEquipment")]
    [InlineData("Workout_Generation_MissingUpperPullBandAnchor")]
    [InlineData("Workout_Generation_MissingDirectCalves")]
    [InlineData("Workout_Generation_MissingDirectRearDelts")]
    [InlineData("Workout_Generation_BelowTargetDynamicCore")]
    [InlineData("Workout_Generation_MissingGluteFocusAbduction")]
    [InlineData("Profile_WorkoutPrefs_HasDumbbells")]
    [InlineData("Profile_WorkoutPrefs_HasBands")]
    [InlineData("Profile_WorkoutPrefs_HasConfirmedBandAnchor")]
    [InlineData("Profile_WorkoutPrefs_HasSliders")]
    [InlineData("Profile_WorkoutPrefs_HomeNeedsEquipment")]
    [InlineData("Workout_Generation_MissingKneeExtension")]
    [InlineData("Workout_Generation_MissingKneeFlexion")]
    [InlineData("Workout_Generation_MissingHipExtension")]
    [InlineData("Workout_Generation_MovementCoverageUnknown")]
    [InlineData("Workout_Generation_OneDayCoverageNotice")]
    [InlineData("Workout_Generation_CoverageGapNotice")]
    [InlineData("Workout_Generation_CoveragePreferredNotice")]
    [InlineData("Workout_Generation_CoverageNoticeTitle")]
    [InlineData("Workout_Generation_DurationOmissionNotice")]
    public void KeyHasNonEmptyEnglishAndRussianCopy(string key)
    {
        var english = Catalog("UiTextCatalog");
        var russian = Catalog("RussianUiText");

        Assert.True(english.TryGetValue(key, out var en) && !string.IsNullOrWhiteSpace(en), $"EN missing {key}");
        Assert.True(russian.TryGetValue(key, out var ru) && !string.IsNullOrWhiteSpace(ru), $"RU missing {key}");
        Assert.Equal(en!.Contains("{0}"), ru!.Contains("{0}"));
    }

    [Fact]
    public void Cycle_phase_training_copy_never_names_soreness_as_a_training_trigger()
    {
        // A-8: ordinary soreness is not a training input, so phase guidance must not tell a user to
        // pull back because of it. PMS symptoms and sleep remain legitimate readiness signals.
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", "ViewModels",
            "Cycle", "CyclePhaseDetailCatalog.cs"));

        Assert.DoesNotContain("soreness", catalog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_map_translates_the_current_luteal_training_tip_without_soreness()
    {
        var runtime = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", "Services",
            "Localization", "UiTextCatalog.RuntimeText.cs"));

        Assert.DoesNotContain("If PMS, sleep, or soreness spikes", runtime, StringComparison.Ordinal);
        var line = runtime.Split('\n')
            .SingleOrDefault(entry => entry.Contains("drop optional work before the crash", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.DoesNotContain("болезнен", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplacementNoticeNoLongerPromisesActivationOnGenerate()
    {
        var en = Catalog("UiTextCatalog")["Workout_Generation_ReplacesActivePlanNotice"];
        Assert.Contains("Apply plan", en, StringComparison.Ordinal);
        Assert.DoesNotContain("replaces your current active plan once", en, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Catalog(string prefix)
    {
        var directory = Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", "Services", "Localization");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, $"{prefix}*.cs"))
        {
            if (file.EndsWith("UiTextCatalog.RuntimeText.cs", StringComparison.Ordinal))
                continue;
            foreach (Match match in Entry.Matches(File.ReadAllText(file)))
                result[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current) && !Directory.Exists(Path.Combine(current, "src", "MuscleCuties.UI")))
            current = Directory.GetParent(current)?.FullName;
        return current ?? throw new InvalidOperationException("Repository root not found.");
    }
}
