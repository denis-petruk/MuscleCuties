namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record SnapshotProgressionSet(int Number, int Repetitions, decimal LoadKg, int? Rir, bool TechniqueAcceptable,
    int? LeftSideReps = null, int? RightSideReps = null);
public sealed record SnapshotProgressionExposure(DateTime CompletedAt, int PrescribedSets, int PrescribedRepetitions,
    int? TargetRir, IReadOnlyList<SnapshotProgressionSet> Sets, bool IsUnilateral = false,
    int? MaximumPrescribedRepetitions = null);

public static class SnapshotProgressionRule
{
    public static StrengthProgressionAdvice Resolve(MajorMuscleGroup muscle, EffectiveExperienceTier tier,
        int exerciseId, int plannedReps, decimal currentLoad, IReadOnlyList<SnapshotProgressionExposure> exposures) =>
        ResolveCore(muscle, tier, exerciseId, plannedReps, currentLoad, exposures);

    private static StrengthProgressionAdvice ResolveCore(MajorMuscleGroup muscle, EffectiveExperienceTier tier,
        int exerciseId, int plannedReps, decimal currentLoad, IReadOnlyList<SnapshotProgressionExposure> exposures)
    {
        if (currentLoad <= 0m) return StrengthProgressionAdviceRule.StartingWeight();
        if (exposures is null || exposures.Count < 2) return StrengthProgressionAdviceRule.LogEachSet();
        var recent = exposures.OrderByDescending(e => e.CompletedAt).Take(2).ToArray();
        if (recent.Any(e => e.PrescribedSets <= 0 || e.PrescribedRepetitions <= 0 ||
            e.MaximumPrescribedRepetitions is { } max && max < e.PrescribedRepetitions || e.TargetRir is < 0 ||
            e.Sets is null || e.Sets.Count != e.PrescribedSets || e.Sets.Any(s => s is null || s.Number <= 0 ||
                s.Repetitions <= 0 || s.LoadKg != currentLoad || s.Rir is < 0 ||
                e.IsUnilateral && (!s.LeftSideReps.HasValue || !s.RightSideReps.HasValue ||
                    s.LeftSideReps <= 0 || s.RightSideReps <= 0)) ||
            e.Sets.Select(s => s.Number).Distinct().Count() != e.PrescribedSets ||
            !e.Sets.Select(s => s.Number).Order().SequenceEqual(Enumerable.Range(1, e.PrescribedSets))))
            return StrengthProgressionAdviceRule.LogEachSet();
        var increment = plannedReps is > 0 and <= StrengthProgressionRule.CompoundRepMaximum ? 2.5m : 1.25m;
        var qualifies = recent.All(e => e.Sets.All(s => s.TechniqueAcceptable &&
            EffectiveRepetitions(s, e.IsUnilateral) >= (e.MaximumPrescribedRepetitions ?? e.PrescribedRepetitions) &&
            (s.Rir is null || e.TargetRir is null || s.Rir >= e.TargetRir)));
        if (qualifies && recent[0].PrescribedSets <= recent[1].PrescribedSets)
            return new StrengthProgressionAdvice(StrengthProgressionAdviceRule.EarnedCode, currentLoad + increment, true, muscle, tier);
        // Gate 73: Two consecutive comparable exposures below minimum prescribed repetitions
        // recommend a load reduction even when optional RIR is not recorded.
        if (recent.All(e => e.Sets.All(s => EffectiveRepetitions(s, e.IsUnilateral) < e.PrescribedRepetitions)))
            return new StrengthProgressionAdvice(
                StrengthProgressionAdviceRule.ReduceCode,
                Math.Max(increment, currentLoad - increment),
                false,
                muscle,
                tier);

        // Preserve the existing reduction behavior when RIR inputs were recorded.
        // The old rule must never be allowed to authorize an increase against fixed rep bands here.
        if (recent.All(e => e.Sets.All(s => s.Rir.HasValue)))
        {
            var priorWeeks = recent.Reverse().Select((e, index) => new DoubleProgressionWeekPerformance(
                index + 1, e.Sets.Select(s => new CompletedWorkingSet(
                    s.Repetitions, s.LoadKg, s.Rir!.Value, s.TechniqueAcceptable)).ToArray())).ToArray();
            var previous = StrengthProgressionAdviceRule.Resolve(muscle, exerciseId, currentLoad, plannedReps, tier, priorWeeks);
            if (previous.RecommendationCode == StrengthProgressionAdviceRule.ReduceCode) return previous;
        }
        return new StrengthProgressionAdvice(StrengthProgressionAdviceRule.MaintainCode, currentLoad, false, muscle, tier);
    }

    private static int EffectiveRepetitions(SnapshotProgressionSet set, bool isUnilateral) =>
        isUnilateral ? Math.Min(set.LeftSideReps!.Value, set.RightSideReps!.Value) : set.Repetitions;
}
