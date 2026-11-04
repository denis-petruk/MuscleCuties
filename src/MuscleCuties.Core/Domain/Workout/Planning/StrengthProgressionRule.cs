namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ExerciseMovementCategory
{
    Compound,
    Accessory
}

public sealed record CompletedWorkingSet(
    int RepsCompleted,
    decimal Load,
    int RirActual,
    bool TechniqueAcceptable = true);

public sealed record SingleSetProgressionResult(
    decimal PrescribedLoad,
    string ProgressionAction, // "Increase", "Maintain", "Decrease"
    string ReasonCode);

public sealed record DoubleProgressionWeekPerformance(
    int WeekNumber,
    IReadOnlyList<CompletedWorkingSet> WorkingSets);

public sealed record MuscleProgressionCandidate(
    MajorMuscleGroup Muscle,
    int ExerciseId,
    decimal CurrentLoad,
    decimal LoadIncrement,
    ExerciseMovementCategory Category,
    IReadOnlyList<DoubleProgressionWeekPerformance> RecentWeeks,
    int CurrentWeeklyVolume,
    int PreviousWeeklyVolume);

public sealed record MuscleProgressionDecision(
    MajorMuscleGroup Muscle,
    int ExerciseId,
    decimal PrescribedLoad,
    bool LoadIncreased,
    string ProgressionStatus,
    string ReasonCode);

public static class StrengthProgressionRule
{
    public const int CompoundRepMinimum = 6;
    public const int CompoundRepMaximum = 12;

    public const int AccessoryRepMinimum = 10;
    public const int AccessoryRepMaximum = 20;

    public const int DefaultTargetRepsInReserve = 2;
    public const int ConservativeTargetRepsInReserve = 3;

    public const int Tier1MaxConcurrentMuscleProgressions = 1;
    public const int Tier2MaxConcurrentMuscleProgressions = 2;
    public const int Tier3MaxConcurrentMuscleProgressions = 3;

    public const int TwoConsecutiveWeeksForDoubleProgression = 2;
    public const decimal MinimumLoad = 0m;

    public const string ActionIncrease = "Increase";
    public const string ActionMaintain = "Maintain";
    public const string ActionDecrease = "Decrease";

    public const string StatusProgressed = "Progressed";
    public const string StatusMaintained = "Maintained";
    public const string StatusRegressed = "Regressed";
    public const string StatusBlockedByVolume = "BlockedByVolume";
    public const string StatusBlockedByTierCap = "BlockedByTierCap";

    public const string DoubleProgressionEarned = "DoubleProgressionEarned";
    public const string RepRangeAndRirSatisfied = "RepRangeAndRirSatisfied";
    public const string RepOrRirDeficitRequiresLoadRegression = "RepOrRirDeficitRequiresLoadRegression";
    public const string UnacceptableTechniqueBlocksProgression = "UnacceptableTechniqueBlocksProgression";
    public const string SimultaneousVolumeIncreaseBlocksLoadProgression = "SimultaneousVolumeIncreaseBlocksLoadProgression";
    public const string ConcurrentProgressionCapExceeded = "ConcurrentProgressionCapExceeded";
    public const string IncompleteHistoryMaintainsLoad = "IncompleteHistoryMaintainsLoad";

    public static (int RepMin, int RepMax, int TargetRir) GetEffortPrescription(
        ExerciseMovementCategory category,
        bool useConservativeRir = false)
    {
        var targetRir = useConservativeRir ? ConservativeTargetRepsInReserve : DefaultTargetRepsInReserve;
        return category == ExerciseMovementCategory.Compound
            ? (CompoundRepMinimum, CompoundRepMaximum, targetRir)
            : (AccessoryRepMinimum, AccessoryRepMaximum, targetRir);
    }

    public static SingleSetProgressionResult ResolveSingleSetLoad(
        int repsPrevious,
        decimal loadPrevious,
        int rirPrevious,
        decimal loadIncrement,
        int repLow,
        int repHigh,
        int targetRir,
        bool techniqueAcceptable = true)
    {
        var safePreviousLoad = Math.Max(MinimumLoad, loadPrevious);
        var safeIncrement = Math.Max(MinimumLoad, loadIncrement);

        if (!techniqueAcceptable)
        {
            if (repsPrevious < repLow || rirPrevious < targetRir)
            {
                var regressedLoad = Math.Max(MinimumLoad, safePreviousLoad - safeIncrement);
                return new SingleSetProgressionResult(regressedLoad, ActionDecrease, RepOrRirDeficitRequiresLoadRegression);
            }

            return new SingleSetProgressionResult(safePreviousLoad, ActionMaintain, UnacceptableTechniqueBlocksProgression);
        }

        if (repsPrevious >= repHigh && rirPrevious >= targetRir)
        {
            var advancedLoad = safePreviousLoad + safeIncrement;
            return new SingleSetProgressionResult(advancedLoad, ActionIncrease, DoubleProgressionEarned);
        }

        if (repsPrevious >= repLow && rirPrevious >= targetRir)
        {
            return new SingleSetProgressionResult(safePreviousLoad, ActionMaintain, RepRangeAndRirSatisfied);
        }

        var decreased = Math.Max(MinimumLoad, safePreviousLoad - safeIncrement);
        return new SingleSetProgressionResult(decreased, ActionDecrease, RepOrRirDeficitRequiresLoadRegression);
    }

    public static int GetConcurrentProgressionLimit(EffectiveExperienceTier tier) => tier switch
    {
        EffectiveExperienceTier.Tier1BeginnerOrNew => Tier1MaxConcurrentMuscleProgressions,
        EffectiveExperienceTier.Tier2IntermediateOrEstablished => Tier2MaxConcurrentMuscleProgressions,
        EffectiveExperienceTier.Tier3Advanced => Tier3MaxConcurrentMuscleProgressions,
        _ => Tier1MaxConcurrentMuscleProgressions
    };

    public static IReadOnlyList<MuscleProgressionDecision> ResolveDoubleProgression(
        EffectiveExperienceTier tier,
        IReadOnlyList<MuscleProgressionCandidate> candidates)
    {
        if (candidates is null || candidates.Count == 0)
            return Array.Empty<MuscleProgressionDecision>();

        var progressionCap = GetConcurrentProgressionLimit(tier);
        var decisions = new List<MuscleProgressionDecision>();
        var progressedCount = 0;

        foreach (var candidate in candidates)
        {
            var (repLow, repHigh, targetRir) = GetEffortPrescription(candidate.Category);
            var safeCurrentLoad = Math.Max(MinimumLoad, candidate.CurrentLoad);
            var safeIncrement = Math.Max(MinimumLoad, candidate.LoadIncrement);

            // Check if 2 consecutive weeks exist
            if (candidate.RecentWeeks is null || candidate.RecentWeeks.Count < TwoConsecutiveWeeksForDoubleProgression)
            {
                decisions.Add(new MuscleProgressionDecision(
                    candidate.Muscle,
                    candidate.ExerciseId,
                    safeCurrentLoad,
                    LoadIncreased: false,
                    StatusMaintained,
                    IncompleteHistoryMaintainsLoad));
                continue;
            }

            var qualifyingWeeks = candidate.RecentWeeks
                .TakeLast(TwoConsecutiveWeeksForDoubleProgression)
                .ToList();

            var anyUnderperformed = false;
            var allWorkingSetsQualified = true;

            foreach (var week in qualifyingWeeks)
            {
                if (week.WorkingSets is null || week.WorkingSets.Count == 0)
                {
                    allWorkingSetsQualified = false;
                    break;
                }

                foreach (var set in week.WorkingSets)
                {
                    if (!set.TechniqueAcceptable || set.RepsCompleted < repLow || set.RirActual < targetRir)
                        anyUnderperformed = true;

                    if (!set.TechniqueAcceptable || set.RepsCompleted < repHigh || set.RirActual < targetRir)
                        allWorkingSetsQualified = false;
                }
            }

            if (allWorkingSetsQualified)
            {
                // Guard: Simultaneous volume increase blocks load progression
                if (candidate.CurrentWeeklyVolume > candidate.PreviousWeeklyVolume)
                {
                    decisions.Add(new MuscleProgressionDecision(
                        candidate.Muscle,
                        candidate.ExerciseId,
                        safeCurrentLoad,
                        LoadIncreased: false,
                        StatusBlockedByVolume,
                        SimultaneousVolumeIncreaseBlocksLoadProgression));
                    continue;
                }

                // Guard: Tier-capped concurrent muscle progressions
                if (progressedCount >= progressionCap)
                {
                    decisions.Add(new MuscleProgressionDecision(
                        candidate.Muscle,
                        candidate.ExerciseId,
                        safeCurrentLoad,
                        LoadIncreased: false,
                        StatusBlockedByTierCap,
                        ConcurrentProgressionCapExceeded));
                    continue;
                }

                progressedCount++;
                decisions.Add(new MuscleProgressionDecision(
                    candidate.Muscle,
                    candidate.ExerciseId,
                    safeCurrentLoad + safeIncrement,
                    LoadIncreased: true,
                    StatusProgressed,
                    DoubleProgressionEarned));
                continue;
            }

            if (anyUnderperformed)
            {
                var regressed = Math.Max(MinimumLoad, safeCurrentLoad - safeIncrement);
                decisions.Add(new MuscleProgressionDecision(
                    candidate.Muscle,
                    candidate.ExerciseId,
                    regressed,
                    LoadIncreased: false,
                    StatusRegressed,
                    RepOrRirDeficitRequiresLoadRegression));
                continue;
            }

            decisions.Add(new MuscleProgressionDecision(
                candidate.Muscle,
                candidate.ExerciseId,
                safeCurrentLoad,
                LoadIncreased: false,
                StatusMaintained,
                RepRangeAndRirSatisfied));
        }

        return decisions;
    }
}
