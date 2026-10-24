namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum MajorMuscleGroup
{
    Chest,
    Back,
    Shoulders,
    Quads,
    Glutes,
    Hamstrings,
    Arms,
    Calves,
    Core
}

public sealed record PlannedExerciseSets(
    int ExerciseId,
    MajorMuscleGroup PrimaryMuscle,
    int Sets);

public sealed record MuscleVolumeStatus(
    MajorMuscleGroup Muscle,
    int DirectSets,
    int MinimumSets,
    int MaximumSets,
    bool IsWithinBounds,
    bool IsBelowMinimum,
    bool IsAboveMaximum);

public sealed record WeeklyHypertrophyVolumeResult(
    EffectiveExperienceTier Tier,
    IReadOnlyDictionary<MajorMuscleGroup, int> VolumeByMuscle,
    IReadOnlyList<MuscleVolumeStatus> MuscleStatuses,
    bool AllTargetedMusclesWithinBounds,
    IReadOnlyList<string> LimitationCodes);

public static class WeeklyHypertrophyVolumeRule
{
    public const int Tier1MinimumDirectSets = 4;
    public const int Tier1MaximumDirectSets = 8;

    public const int Tier2MinimumDirectSets = 8;
    public const int Tier2MaximumDirectSets = 16;

    public const int Tier3MinimumDirectSets = 12;
    public const int Tier3MaximumDirectSets = 22;

    public const int NoDirectSets = 0;

    public const string VolumeBelowTierMinimum = "WeeklyVolumeBelowTierMinimum";
    public const string VolumeExceedsTierMaximum = "WeeklyVolumeExceedsTierMaximum";
    public const string ExerciseSetsMustBePositive = "ExerciseSetsMustBePositive";

    public static (int Min, int Max) GetBounds(EffectiveExperienceTier tier) => tier switch
    {
        EffectiveExperienceTier.Tier1BeginnerOrNew => (Tier1MinimumDirectSets, Tier1MaximumDirectSets),
        EffectiveExperienceTier.Tier2IntermediateOrEstablished => (Tier2MinimumDirectSets, Tier2MaximumDirectSets),
        EffectiveExperienceTier.Tier3Advanced => (Tier3MinimumDirectSets, Tier3MaximumDirectSets),
        _ => (Tier1MinimumDirectSets, Tier1MaximumDirectSets)
    };

    public static IReadOnlyDictionary<MajorMuscleGroup, int> CalculateVolume(
        IEnumerable<PlannedExerciseSets>? exercises)
    {
        var volume = new Dictionary<MajorMuscleGroup, int>();
        foreach (var muscle in Enum.GetValues<MajorMuscleGroup>())
            volume[muscle] = NoDirectSets;

        if (exercises is null)
            return volume;

        foreach (var exercise in exercises)
        {
            if (exercise is null || !Enum.IsDefined(exercise.PrimaryMuscle) || exercise.Sets <= NoDirectSets)
                continue;

            volume[exercise.PrimaryMuscle] += exercise.Sets;
        }

        return volume;
    }

    public static WeeklyHypertrophyVolumeResult Evaluate(
        EffectiveExperienceTier tier,
        IEnumerable<PlannedExerciseSets>? exercises,
        IReadOnlySet<MajorMuscleGroup>? targetedMuscles = null)
    {
        var (minSets, maxSets) = GetBounds(tier);
        var volume = CalculateVolume(exercises);
        var statuses = new List<MuscleVolumeStatus>();
        var limitations = new List<string>();
        var allWithinBounds = true;

        var musclesToEvaluate = targetedMuscles ?? Enum.GetValues<MajorMuscleGroup>().ToHashSet();

        foreach (var muscle in musclesToEvaluate.OrderBy(m => (int)m))
        {
            var directSets = volume.GetValueOrDefault(muscle, NoDirectSets);
            var isBelow = directSets < minSets;
            var isAbove = directSets > maxSets;
            var isWithin = !isBelow && !isAbove;

            if (!isWithin)
                allWithinBounds = false;

            if (isBelow)
                limitations.Add($"{VolumeBelowTierMinimum}_{muscle}");
            if (isAbove)
                limitations.Add($"{VolumeExceedsTierMaximum}_{muscle}");

            statuses.Add(new MuscleVolumeStatus(
                muscle,
                directSets,
                minSets,
                maxSets,
                isWithin,
                isBelow,
                isAbove));
        }

        return new WeeklyHypertrophyVolumeResult(
            tier,
            volume,
            statuses,
            allWithinBounds,
            limitations);
    }
}
