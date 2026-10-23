using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Deterministic lookups over the persistence-neutral planning snapshot.</summary>
public sealed class WorkoutPlanningReferenceIndex
{
    private readonly IReadOnlyDictionary<(int ExerciseId, int MuscleGroupId), double> _contributions;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<PlanningMuscleContribution>> _byExercise;

    public WorkoutPlanningReferenceIndex(WorkoutPlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var contributionRows = snapshot.ExerciseDefinitions
            .Where(definition => definition.Id > 0 && definition.MuscleContributions is not null)
            .SelectMany(definition => definition.MuscleContributions!
                .Where(row => row is not null &&
                              row.ExerciseDefinitionId == definition.Id &&
                              row.MuscleGroupId > 0))
            .Where(row => row is not null && double.IsFinite(row.Fraction) && row.Fraction >= 0)
            .ToArray();

        _contributions = contributionRows
            .GroupBy(row => (row.ExerciseDefinitionId, row.MuscleGroupId))
            .ToDictionary(group => group.Key, group => group.First().Fraction);
        _byExercise = contributionRows
            .GroupBy(row => row.ExerciseDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlanningMuscleContribution>)group.ToArray());
    }

    /// <summary>Builds the contribution portion of the index from neutral contribution records.</summary>
    public WorkoutPlanningReferenceIndex(IReadOnlyList<PlanningMuscleContribution>? contributions)
    {
        var rows = (contributions ?? [])
            .Where(row => row is not null && row.ExerciseDefinitionId > 0 && row.MuscleGroupId > 0 &&
                          double.IsFinite(row.Fraction) && row.Fraction >= 0)
            .ToArray();

        _contributions = rows
            .GroupBy(row => (row.ExerciseDefinitionId, row.MuscleGroupId))
            .ToDictionary(group => group.Key, group => group.First().Fraction);
        _byExercise = rows
            .GroupBy(row => row.ExerciseDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlanningMuscleContribution>)group.ToArray());
    }

    public double Contribution(int exerciseDefinitionId, int muscleGroupId) =>
        _contributions.GetValueOrDefault((exerciseDefinitionId, muscleGroupId));

    public IReadOnlyList<PlanningMuscleContribution> ContributionsFor(int exerciseDefinitionId) =>
        _byExercise.GetValueOrDefault(exerciseDefinitionId, []);

    public IEnumerable<int> ExerciseIds() => _byExercise.Keys;

    public static HashSet<int> GoalMuscles(
        WorkoutPlanningSnapshot snapshot,
        UserGoal goal,
        bool tierCOnly) => snapshot.GoalTierWeights
        .Where(weight => weight.Goal == goal && (tierCOnly ? weight.Tier == PriorityTier.C : weight.Tier != PriorityTier.C))
        .Select(weight => weight.MuscleGroupId)
        .ToHashSet();

    public static Dictionary<int, double> VolumeRows(WorkoutPlanningSnapshot snapshot, int daysPerWeek) =>
        VolumeRows(snapshot.VolumeBudgets, daysPerWeek);

    public static Dictionary<int, double> VolumeRows(
        IReadOnlyList<PlanningVolumeBudget>? volumeBudgets,
        int daysPerWeek)
    {
        var rowsByMuscle = new Dictionary<int, double>();
        foreach (var row in volumeBudgets ?? [])
        {
            if (row.DaysPerWeek != daysPerWeek || row.MuscleGroupId <= 0 ||
                !double.IsFinite(row.FractionalSets) || row.FractionalSets < 0)
                continue;

            // Match the legacy resolver's dictionary assignment: the last valid row wins.
            rowsByMuscle[row.MuscleGroupId] = row.FractionalSets;
        }

        return rowsByMuscle;
    }
}
