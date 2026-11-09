using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ExerciseSwapEligibilityRuleTests
{
    private const int LatPulldown = 142;
    private const int PullUp = 141;
    private const int StraightArmPulldown = 146;
    private const int DumbbellRow = 145;
    private const int ChestSupportedRow = 143;
    private const int SeatedCableRow = 144;
    private const int RomanianDeadlift = 111;
    private const int DumbbellRdl = 113;
    private const int DeficitRdl = 112;
    private const int GoodMorning = 118;

    private static ExerciseSwapEligibilityInput Input(int original, Equipment? equipment = Equipment.FullGym,
        ActivityExperience experience = ActivityExperience.Beginner, IReadOnlySet<MovementPattern>? blocked = null,
        InjuryFlag flags = InjuryFlag.None, IReadOnlyList<StrengthExerciseCatalogEntry>? catalog = null,
        IReadOnlySet<int>? excluded = null) =>
        new(original, catalog ?? ReviewedCatalogFixture.Catalog, equipment, experience, excluded ?? new HashSet<int>(),
            blocked ?? new HashSet<MovementPattern>(), flags);

    private static int[] Ids(ExerciseSwapEligibilityResult result) =>
        result.Candidates.Select(candidate => candidate.Entry.Candidate.ExerciseId).ToArray();

    [Fact]
    public void Full_gym_offers_only_reviewed_close_alternatives()
    {
        var result = ExerciseSwapEligibilityRule.Evaluate(Input(LatPulldown));

        Assert.Equal([PullUp], Ids(result));
        Assert.DoesNotContain(StraightArmPulldown, Ids(result)); // Rejected pair.
        Assert.All(result.Candidates, candidate => Assert.False(string.IsNullOrWhiteSpace(candidate.Conditions)));
    }

    [Fact]
    public void Bodyweight_and_home_profiles_never_offer_a_pull_up_without_a_bar()
    {
        foreach (var equipment in new[] { Equipment.Bodyweight, Equipment.HomeDumbbellsBands })
        {
            var result = ExerciseSwapEligibilityRule.Evaluate(Input(LatPulldown, equipment));
            Assert.DoesNotContain(PullUp, Ids(result));
            Assert.Contains(ExerciseSwapEligibilityRule.NoEligibleReviewedSubstituteCode, result.ReasonCodes);
        }
    }

    [Fact]
    public void Unknown_equipment_profile_yields_no_candidates_instead_of_a_full_gym_default()
    {
        var result = ExerciseSwapEligibilityRule.Evaluate(Input(LatPulldown, equipment: null));

        Assert.Empty(result.Candidates);
        Assert.Equal([ExerciseSwapEligibilityRule.EquipmentProfileMissingCode], result.ReasonCodes);
        Assert.False(result.Allows(PullUp));
    }

    [Fact]
    public void Manual_programming_pairs_are_never_offered_so_established_only_hinges_stay_out()
    {
        var result = ExerciseSwapEligibilityRule.Evaluate(Input(RomanianDeadlift,
            experience: ActivityExperience.Established));

        Assert.Equal([DumbbellRdl], Ids(result));
        Assert.DoesNotContain(DeficitRdl, Ids(result));
        Assert.DoesNotContain(GoodMorning, Ids(result));
    }

    [Fact]
    public void Beginner_never_receives_an_established_only_close_alternative()
    {
        // Synthetic close pair to an Established-only exercise isolates the experience filter.
        var original = ReviewedCatalogFixture.Entry(RomanianDeadlift);
        var deficit = ReviewedCatalogFixture.Entry(DeficitRdl);
        var syntheticOriginal = original with
        {
            Candidate = original.Candidate with
            {
                DirectedSubstitutionReviews =
                [
                    new DirectedSubstitutionReview(DeficitRdl, DirectedSubstitutionCategory.CloseProgrammingAlternative,
                        "Synthetic close pair for tests.")
                ]
            }
        };
        IReadOnlyList<StrengthExerciseCatalogEntry> catalog = [syntheticOriginal, deficit];

        Assert.Empty(ExerciseSwapEligibilityRule.Evaluate(Input(RomanianDeadlift, catalog: catalog)).Candidates);
        Assert.Empty(ExerciseSwapEligibilityRule.Evaluate(Input(RomanianDeadlift,
            experience: ActivityExperience.Returning, catalog: catalog)).Candidates);
        Assert.Equal([DeficitRdl], Ids(ExerciseSwapEligibilityRule.Evaluate(Input(RomanianDeadlift,
            experience: ActivityExperience.Established, catalog: catalog))));
    }

    [Fact]
    public void Blocked_movement_pattern_excludes_every_alternative_with_that_pattern()
    {
        var result = ExerciseSwapEligibilityRule.Evaluate(Input(DumbbellRow,
            blocked: new HashSet<MovementPattern> { MovementPattern.HorizontalPull }));

        Assert.Empty(result.Candidates);
        Assert.Equal([ChestSupportedRow, SeatedCableRow],
            Ids(ExerciseSwapEligibilityRule.Evaluate(Input(DumbbellRow))));
    }

    [Fact]
    public void Unanswered_injury_trigger_keeps_site_contraindications()
    {
        // An unanswered worsening trigger blocks no pattern, but the active site still excludes
        // contraindicated alternatives (Seated Cable Row is contraindicated for flag 16).
        var answers = new[] { new InjurySafetyAnswer(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
            null, (InjuryFlag)16) };
        var blocked = InjuryWorseningMovementRule.BlockedPatterns(answers, []);
        var result = ExerciseSwapEligibilityRule.Evaluate(Input(DumbbellRow, blocked: blocked, flags: (InjuryFlag)16));

        Assert.Empty(blocked);
        Assert.Equal([ChestSupportedRow], Ids(result));
    }

    [Fact]
    public void Excluded_exercises_and_unreviewed_originals_are_never_offered()
    {
        Assert.Empty(ExerciseSwapEligibilityRule.Evaluate(Input(LatPulldown,
            excluded: new HashSet<int> { PullUp })).Candidates);
        var unreviewed = ExerciseSwapEligibilityRule.Evaluate(Input(9999));
        Assert.Equal([ExerciseSwapEligibilityRule.OriginalExerciseNotReviewedCode], unreviewed.ReasonCodes);
    }

    [Fact]
    public void Swap_experience_matches_the_planner_and_defaults_unknown_strength_history_to_beginner()
    {
        var date = new DateOnly(2026, 11, 8);
        Assert.Equal(ActivityExperience.Beginner, ExerciseSwapEligibilityRule.Experience(
            WorkoutActivityType.GeneralStrength, null, date, null));
        Assert.Equal(ActivityExperience.Beginner, ExerciseSwapEligibilityRule.Experience(
            WorkoutActivityType.GeneralStrength, null, date, TrainingExperienceLevel.Beginner));
        var beginnerHistory = new ActivityTrainingHistory(nameof(WorkoutActivityType.GeneralStrength), 2, 1m,
            date.AddDays(-2), true);
        Assert.Equal(ActivityExperience.Beginner, ExerciseSwapEligibilityRule.Experience(
            WorkoutActivityType.GeneralStrength, beginnerHistory, date, TrainingExperienceLevel.Intermediate));
    }
}
