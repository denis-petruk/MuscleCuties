using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum LowerBodyMuscleGroup
{
    Glutes,
    Hamstrings,
    Abductors,
    Adductors
}

public sealed record LowerBodyCoverageResult(
    IReadOnlyDictionary<LowerBodyMuscleGroup, int> SessionExposures,
    IReadOnlyList<LowerBodyMuscleGroup> BelowMinimum)
{
    public bool MeetsMinimum => BelowMinimum.Count == 0;
}

/// <summary>
/// Owner rule: glutes, hamstrings, abductors and adductors each appear in at least two training
/// sessions per week. The remaining muscle groups stay best effort.
/// </summary>
public static class LowerBodyCoverageRule
{
    public const int MinimumWeeklySessionExposures = 2;

    /// <summary>Ranks above <see cref="GoalSpecificAccessoryPriorityRule"/>'s strongest tier score.</summary>
    public const int CoveragePriorityScore = 10;

    public const string CoverageGapCodePrefix = "LowerBodyCoverageBelowMinimum";

    private static readonly IReadOnlyList<LowerBodyMuscleGroup> OrderedMuscles =
        [LowerBodyMuscleGroup.Glutes, LowerBodyMuscleGroup.Hamstrings,
            LowerBodyMuscleGroup.Abductors, LowerBodyMuscleGroup.Adductors];

    private static readonly string[] GlutesNames = ["Glutes", "GluteMax", "GluteMaximus", "GluteusMaximus"];
    private static readonly string[] HamstringsNames = ["Hamstrings", "Hamstring"];
    private static readonly string[] AbductorsNames = ["GluteMed", "GluteMedius", "Abductors", "HipAbductors"];
    private static readonly string[] AdductorsNames = ["Adductors", "Adductor", "HipAdductors"];

    private static readonly IReadOnlySet<MovementPattern> AccessoryCoveragePatterns =
        new HashSet<MovementPattern>
        {
            MovementPattern.HipAbduction,
            MovementPattern.HipAdduction,
            MovementPattern.KneeFlexion
        };

    public static IReadOnlyList<LowerBodyMuscleGroup> MuscleGroups => OrderedMuscles;

    public static LowerBodyCoverageResult Evaluate(
        IEnumerable<IReadOnlyCollection<string>?>? sessionMuscleGroups)
    {
        var exposures = OrderedMuscles.ToDictionary(muscle => muscle, _ => 0);
        if (sessionMuscleGroups is not null)
            foreach (var session in sessionMuscleGroups)
            {
                if (session is null) continue;
                foreach (var muscle in OrderedMuscles)
                    if (session.Any(muscleName => Matches(muscle, muscleName)))
                        exposures[muscle]++;
            }

        return new LowerBodyCoverageResult(exposures,
            OrderedMuscles.Where(muscle => exposures[muscle] < MinimumWeeklySessionExposures).ToArray());
    }

    public static string GapCode(LowerBodyMuscleGroup muscle) => $"{CoverageGapCodePrefix}_{muscle}";

    /// <summary>
    /// Catalog exercises that could lift a below-minimum muscle through an accessory slot.
    /// The resolver still applies equipment, injury and review filtering, so this only reorders
    /// candidates that were already safe.
    /// </summary>
    public static IReadOnlySet<int> PriorityExerciseIds(
        IEnumerable<LowerBodyMuscleGroup> belowMinimum,
        IReadOnlyDictionary<int, IReadOnlySet<string>>? muscleGroupsByExerciseId,
        IReadOnlyList<StrengthExerciseCatalogEntry>? catalog)
    {
        var ids = new HashSet<int>();
        if (muscleGroupsByExerciseId is null || catalog is null) return ids;

        var gaps = belowMinimum.ToArray();
        foreach (var entry in catalog)
        {
            if (entry?.Candidate is null) continue;
            if (!AccessoryCoveragePatterns.Contains(entry.Candidate.Pattern)) continue;
            if (!muscleGroupsByExerciseId.TryGetValue(entry.Candidate.ExerciseId, out var groups) ||
                groups is null)
                continue;

            if (gaps.Any(gap => groups.Any(muscleName => Matches(gap, muscleName))))
                ids.Add(entry.Candidate.ExerciseId);
        }

        return ids;
    }

    private static bool Matches(LowerBodyMuscleGroup muscle, string? muscleName) =>
        !string.IsNullOrWhiteSpace(muscleName) &&
        NamesFor(muscle).Contains(muscleName.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string[] NamesFor(LowerBodyMuscleGroup muscle) => muscle switch
    {
        LowerBodyMuscleGroup.Glutes => GlutesNames,
        LowerBodyMuscleGroup.Hamstrings => HamstringsNames,
        LowerBodyMuscleGroup.Abductors => AbductorsNames,
        LowerBodyMuscleGroup.Adductors => AdductorsNames,
        _ => []
    };
}
