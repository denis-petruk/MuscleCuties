using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Reports observed movement-pattern gaps; it does not select or prescribe exercises.</summary>
public static class WeeklyMovementCoverageRule
{
    public const string MissingKneeExtension = "WeeklyMovementCoverageMissing_KneeExtension";
    public const string MissingKneeFlexion = "WeeklyMovementCoverageMissing_KneeFlexion";
    public const string MissingHipExtension = "WeeklyMovementCoverageMissing_HipExtension";
    public const string UnknownCoverage = "WeeklyMovementCoverageUnknown";

    public static IReadOnlyList<string> MissingPatterns(
        IReadOnlyCollection<int> selectedExerciseIds, IReadOnlyList<StrengthExerciseCatalogEntry> catalog)
    {
        if (selectedExerciseIds.Count == 0) return [];
        var byId = catalog.Where(entry => entry?.Candidate is not null)
            .GroupBy(entry => entry.Candidate.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Candidate.Pattern).Distinct().ToArray());
        if (selectedExerciseIds.Any(id => !byId.TryGetValue(id, out var patterns) || patterns.Length != 1 ||
                                          !Enum.IsDefined(patterns[0])))
            return [UnknownCoverage];
        var selected = selectedExerciseIds.Select(id => byId[id][0]).ToHashSet();
        var missing = new List<string>();
        if (!selected.Contains(MovementPattern.SquatPattern) && !selected.Contains(MovementPattern.Lunge))
            missing.Add(MissingKneeExtension);
        if (!selected.Contains(MovementPattern.KneeFlexion))
            missing.Add(MissingKneeFlexion);
        if (!selected.Contains(MovementPattern.HipHinge) && !selected.Contains(MovementPattern.HipThrust))
            missing.Add(MissingHipExtension);
        return missing;
    }
}
