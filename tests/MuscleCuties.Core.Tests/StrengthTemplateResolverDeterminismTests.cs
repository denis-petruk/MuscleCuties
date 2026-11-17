using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// P2 canonical planner: exercise selection must be reproducible from normalised inputs alone. The
/// catalog reaches the resolver in whatever order persistence returned it, so the same exercises in a
/// different order must produce the same prescription; an ambiguous catalog (two entries claiming one
/// exercise id) must fail closed instead of resolving by whichever row arrived first.
/// </summary>
public sealed class StrengthTemplateResolverDeterminismTests
{
    private const int MaximumWorkingExercises = 5;

    [Fact]
    public void Catalog_order_never_changes_the_resolved_prescription()
    {
        StrengthExerciseCatalogEntry[] entries =
        [
            Candidate(11, MovementPattern.Lunge),
            Candidate(12, MovementPattern.SquatPattern),
            Candidate(21, MovementPattern.HipThrust),
            Candidate(31, MovementPattern.VerticalPush),
            Candidate(41, MovementPattern.VerticalPull)
        ];

        var baseline = Describe(StrengthTemplateResolver.Resolve(Input(entries), MaximumWorkingExercises));
        var permutations = Permutations(entries).ToArray();

        Assert.Equal(120, permutations.Length);
        foreach (var permutation in permutations)
            Assert.Equal(baseline, Describe(StrengthTemplateResolver.Resolve(Input(permutation), MaximumWorkingExercises)));
    }

    [Fact]
    public void A_catalog_with_a_duplicate_exercise_id_fails_closed_instead_of_resolving_by_row_order()
    {
        var original = Candidate(11, MovementPattern.Lunge);
        var impostor = original with { Name = "Impostor 11" };
        StrengthExerciseCatalogEntry[] rest =
        [
            Candidate(21, MovementPattern.HipThrust),
            Candidate(31, MovementPattern.VerticalPush),
            Candidate(41, MovementPattern.VerticalPull)
        ];

        var first = StrengthTemplateResolver.Resolve(Input([original, impostor, .. rest]), MaximumWorkingExercises);
        var reversed = StrengthTemplateResolver.Resolve(Input([impostor, original, .. rest]), MaximumWorkingExercises);

        // The ambiguity must never reach a prescription: the same exercise id cannot resolve to two
        // different names depending on which row the database returned first.
        Assert.Equal("Invalid", first.Status);
        Assert.Equal("Invalid", reversed.Status);
        Assert.Contains("DuplicateCatalogExerciseId", first.LimitationCodes);
        Assert.Equal(first.Slots.Count, reversed.Slots.Count);
    }

    private static string Describe(StrengthTemplateResolution resolution) =>
        string.Join("|", resolution.Slots.Select(slot =>
            $"{slot.SlotId}:{slot.ExerciseId}:{slot.ExerciseName}:{slot.Sets}:{slot.RepetitionsInReserve}"));

    private static IEnumerable<StrengthExerciseCatalogEntry[]> Permutations(StrengthExerciseCatalogEntry[] source)
    {
        if (source.Length <= 1)
        {
            yield return source;
            yield break;
        }

        for (var index = 0; index < source.Length; index++)
        {
            var head = source[index];
            var rest = source.Where((_, position) => position != index).ToArray();
            foreach (var tail in Permutations(rest))
                yield return [head, .. tail];
        }
    }

    private static StrengthTemplateSelectionInput Input(IReadOnlyList<StrengthExerciseCatalogEntry> catalog)
    {
        const WorkoutActivityType activity = WorkoutActivityType.GeneralStrength;
        const ActivityExperience experience = ActivityExperience.Beginner;
        return new StrengthTemplateSelectionInput(
            StrengthTemplateCatalog.ForSchedule(1).Single(),
            activity,
            UserGoal.Strength,
            new ActivityPrescriptionContext(activity, experience, null, false, true, false, 0, false, false),
            catalog,
            new ExerciseEligibilityContext(
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                experience,
                new HashSet<int>(),
                new HashSet<MovementPattern>(),
                new HashSet<ActivityDemandTag>(),
                false),
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new HashSet<int>());
    }

    private static StrengthExerciseCatalogEntry Candidate(int id, MovementPattern pattern) =>
        new($"Exercise {id}",
            new ReviewedExerciseCandidate(id, "test-v2", pattern,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established },
                new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
                true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1m, new HashSet<int>(), InjuryFlag.None), []);
}
