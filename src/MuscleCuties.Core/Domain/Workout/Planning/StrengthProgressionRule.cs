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

    /// <summary>
    /// Owner decision OD-6 (option b): a load decrease needs this many consecutive comparable,
    /// fully logged under-target sessions. One under-target session holds the load. Skipped and
    /// incomplete sessions are not comparable evidence and never count toward the streak.
    /// </summary>
    public const int ConsecutiveUnderTargetSessionsForLoadDecrease = 2;
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
    public const string SingleUnderTargetSessionHoldsLoad = "SingleUnderTargetSessionHoldsLoad";
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

    /// <summary>
    /// Resolves the next load from one comparable session. An under-target result (reps below the
    /// range or effort harder than the target) lowers the load only when the previous comparable,
    /// fully logged session was also under target; otherwise the load is held. Callers must not pass
    /// a skipped or incomplete session as the previous comparable session.
    /// </summary>
    public static SingleSetProgressionResult ResolveSingleSetLoad(
        int repsPrevious,
        decimal loadPrevious,
        int rirPrevious,
        decimal loadIncrement,
        int repLow,
        int repHigh,
        int targetRir,
        bool techniqueAcceptable = true,
        bool previousComparableSessionUnderTarget = false)
    {
        var safePreviousLoad = Math.Max(MinimumLoad, loadPrevious);
        var safeIncrement = Math.Max(MinimumLoad, loadIncrement);
        var underTarget = repsPrevious < repLow || rirPrevious < targetRir;

        if (underTarget)
        {
            if (!previousComparableSessionUnderTarget)
                return new SingleSetProgressionResult(safePreviousLoad, ActionMaintain, SingleUnderTargetSessionHoldsLoad);

            var regressedLoad = Math.Max(MinimumLoad, safePreviousLoad - safeIncrement);
            return new SingleSetProgressionResult(regressedLoad, ActionDecrease, RepOrRirDeficitRequiresLoadRegression);
        }

        if (!techniqueAcceptable)
            return new SingleSetProgressionResult(safePreviousLoad, ActionMaintain, UnacceptableTechniqueBlocksProgression);

        if (repsPrevious >= repHigh)
        {
            var advancedLoad = safePreviousLoad + safeIncrement;
            return new SingleSetProgressionResult(advancedLoad, ActionIncrease, DoubleProgressionEarned);
        }

        return new SingleSetProgressionResult(safePreviousLoad, ActionMaintain, RepRangeAndRirSatisfied);
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

            var allWorkingSetsQualified = true;
            var underTargetWeeks = 0;

            foreach (var week in qualifyingWeeks)
            {
                // A week without working sets is skipped or incomplete: it neither qualifies
                // nor counts as an under-target result.
                if (week.WorkingSets is null || week.WorkingSets.Count == 0)
                {
                    allWorkingSetsQualified = false;
                    continue;
                }

                if (week.WorkingSets.Any(set => set is null || !set.TechniqueAcceptable ||
                        set.RepsCompleted < repLow || set.RirActual < targetRir))
                    underTargetWeeks++;

                if (week.WorkingSets.Any(set => set is null || !set.TechniqueAcceptable ||
                        set.RepsCompleted < repHigh || set.RirActual < targetRir))
                    allWorkingSetsQualified = false;
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

            if (underTargetWeeks >= ConsecutiveUnderTargetSessionsForLoadDecrease)
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

            if (underTargetWeeks > 0)
            {
                decisions.Add(new MuscleProgressionDecision(
                    candidate.Muscle,
                    candidate.ExerciseId,
                    safeCurrentLoad,
                    LoadIncreased: false,
                    StatusMaintained,
                    SingleUnderTargetSessionHoldsLoad));
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
