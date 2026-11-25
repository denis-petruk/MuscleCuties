namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>The major muscle groups weekly hypertrophy volume is judged across.</summary>
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

/// <summary>The sets one exercise contributes to a muscle group in the week.</summary>
public sealed record PlannedExerciseSets(
    int ExerciseId,
    MajorMuscleGroup PrimaryMuscle,
    int Sets);

/// <summary>Whether a muscle group sits under, inside or over its weekly range.</summary>
public sealed record MuscleVolumeStatus(
    MajorMuscleGroup Muscle,
    int DirectSets,
    int MinimumSets,
    int MaximumSets,
    bool IsWithinBounds,
    bool IsBelowMinimum,
    bool IsAboveMaximum);

/// <summary>The week's per-muscle status with the groups that fall outside their range.</summary>
public sealed record WeeklyHypertrophyVolumeResult(
    EffectiveExperienceTier Tier,
    IReadOnlyDictionary<MajorMuscleGroup, int> VolumeByMuscle,
    IReadOnlyList<MuscleVolumeStatus> MuscleStatuses,
    bool AllTargetedMusclesWithinBounds,
    IReadOnlyList<string> LimitationCodes);

/// <summary>Judges the week's volume against the hypertrophy ranges, so a shortfall is named.</summary>
public static class WeeklyHypertrophyVolumeRule
{
    public const int Tier1MinimumDirectSets = Tier1TargetMinSets;
    public const int Tier1MaximumDirectSets = Tier1HardCapSets;

    public const int Tier2MinimumDirectSets = 8;
    public const int Tier2MaximumDirectSets = 16;

    public const int Tier3MinimumDirectSets = 12;
    public const int Tier3MaximumDirectSets = 22;

    // Spec Section 9 Direct Working Sets: Target and Hard Cap per Tier
    public const int Tier1TargetMinSets = 8;
    public const int Tier1TargetMaxSets = 12;
    public const int Tier1HardCapSets = 12;

    public const int Tier2TargetMinSets = 12;
    public const int Tier2TargetMaxSets = 16;
    public const int Tier2HardCapSets = 16;

    public const int Tier3TargetMinSets = 16;
    public const int Tier3TargetMaxSets = 20;
    public const int Tier3HardCapSets = 22;

    public const int NoDirectSets = 0;

    public const string VolumeBelowTierMinimum = "WeeklyVolumeBelowTierMinimum";
    public const string VolumeExceedsTierMaximum = "WeeklyVolumeExceedsTierMaximum";
    public const string ExerciseSetsMustBePositive = "ExerciseSetsMustBePositive";

    /// <summary>The weekly set range the tier should stay inside.</summary>
    public static (int Min, int Max) GetBounds(EffectiveExperienceTier tier) => tier switch
    {
        EffectiveExperienceTier.Tier1BeginnerOrNew => (Tier1MinimumDirectSets, Tier1MaximumDirectSets),
        EffectiveExperienceTier.Tier2IntermediateOrEstablished => (Tier2MinimumDirectSets, Tier2MaximumDirectSets),
        EffectiveExperienceTier.Tier3Advanced => (Tier3MinimumDirectSets, Tier3MaximumDirectSets),
        _ => (Tier1MinimumDirectSets, Tier1MaximumDirectSets)
    };

    /// <summary>The target band and hard cap the tier may not exceed.</summary>
    public static (int TargetMin, int TargetMax, int HardCap) GetTierVolumeBounds(EffectiveExperienceTier tier) => tier switch
    {
        EffectiveExperienceTier.Tier1BeginnerOrNew => (Tier1TargetMinSets, Tier1TargetMaxSets, Tier1HardCapSets),
        EffectiveExperienceTier.Tier2IntermediateOrEstablished => (Tier2TargetMinSets, Tier2TargetMaxSets, Tier2HardCapSets),
        EffectiveExperienceTier.Tier3Advanced => (Tier3TargetMinSets, Tier3TargetMaxSets, Tier3HardCapSets),
        _ => (Tier1TargetMinSets, Tier1TargetMaxSets, Tier1HardCapSets)
    };

    /// <summary>Weekly sets each muscle group receives from the selected exercises.</summary>
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

    /// <summary>Checks the week's volume against the tier's band and cap.</summary>
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
