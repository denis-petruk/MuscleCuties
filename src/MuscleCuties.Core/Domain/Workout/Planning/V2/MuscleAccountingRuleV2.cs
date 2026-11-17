using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

/// <summary>
/// Package C: Separates physical working sets, direct muscle credits (1.0), and indirect
/// stimulus credits (0.5). Computes meaningful exposure days (>= 2 direct working sets on a
/// distinct local calendar date) and evaluates Gates 49-58 coverage rules.
/// </summary>
public static class MuscleAccountingRuleV2
{
    private static readonly MuscleGroupV2[] AllPrimaryMuscles =
    [
        MuscleGroupV2.GluteusMaximus,
        MuscleGroupV2.Quadriceps,
        MuscleGroupV2.Hamstrings,
        MuscleGroupV2.Back,
        MuscleGroupV2.Chest,
        MuscleGroupV2.LateralDelts,
        MuscleGroupV2.RearDelts,
        MuscleGroupV2.Biceps,
        MuscleGroupV2.Triceps,
        MuscleGroupV2.Calves,
        MuscleGroupV2.Core,
        MuscleGroupV2.HipAbductors
    ];

    public static MuscleAccountingSummaryV2 Evaluate(
        IReadOnlyList<PlannedSessionV2> sessions,
        IReadOnlyDictionary<int, ExerciseCatalogItemV2> catalogById,
        TrainingExperienceLevel experience,
        TrainingFocus focus)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(catalogById);

        var directTotals = AllPrimaryMuscles.ToDictionary(m => m, _ => 0m);
        var indirectTotals = AllPrimaryMuscles.ToDictionary(m => m, _ => 0m);
        var directSetsByMuscleAndDate = AllPrimaryMuscles.ToDictionary(
            m => m,
            _ => new Dictionary<DateOnly, decimal>());

        var totalPhysicalWorkingSets = 0;
        var hasKneeDominant = false;
        var hasKneeFlexion = false;
        var hasHipExtension = false;
        var dynamicCoreDates = new HashSet<DateOnly>();

        foreach (var session in sessions)
        {
            foreach (var exercise in session.Exercises)
            {
                if (exercise.WorkingSets <= 0)
                    continue;

                // Unilateral exercises never double physical working-set count; warm-up sets never count.
                totalPhysicalWorkingSets += exercise.WorkingSets;

                if (!catalogById.TryGetValue(exercise.ExerciseId, out var item))
                    continue;

                if (item.Pattern is MovementPattern.SquatPattern or MovementPattern.Lunge)
                    hasKneeDominant = true;
                if (item.Pattern == MovementPattern.KneeFlexion)
                    hasKneeFlexion = true;
                if (item.Pattern is MovementPattern.HipHinge or MovementPattern.HipThrust)
                    hasHipExtension = true;
                if (item.VarietyTag == ExerciseVarietyTag.DynamicCore ||
                    (item.Pattern is MovementPattern.AntiExtension or MovementPattern.AntiRotation or MovementPattern.AntiLateralFlexion &&
                     item.VarietyTag != ExerciseVarietyTag.StaticCore))
                {
                    dynamicCoreDates.Add(session.LocalDate);
                }

                foreach (var contribution in item.MuscleContributions)
                {
                    if (!directTotals.ContainsKey(contribution.Muscle))
                        continue;

                    var sets = exercise.WorkingSets * contribution.CreditPerWorkingSet;
                    if (contribution.Kind == MuscleContributionKind.Direct)
                    {
                        directTotals[contribution.Muscle] += sets;
                        var byDate = directSetsByMuscleAndDate[contribution.Muscle];
                        byDate[session.LocalDate] = byDate.GetValueOrDefault(session.LocalDate) + sets;
                    }
                    else if (contribution.Kind == MuscleContributionKind.Indirect)
                    {
                        indirectTotals[contribution.Muscle] += sets;
                    }
                }
            }
        }

        var entries = new List<MuscleAccountingEntryV2>(AllPrimaryMuscles.Length);
        foreach (var muscle in AllPrimaryMuscles)
        {
            var target = WorkoutPolicyV2.GetWeeklyVolumeTarget(muscle, experience, focus);
            var meaningfulDays = directSetsByMuscleAndDate[muscle].Values
                .Count(setsOnDate => setsOnDate >= WorkoutPolicyV2.MeaningfulExposureMinimumDirectSets);
            var direct = directTotals[muscle];
            var indirect = indirectTotals[muscle];
            var meetsTarget = target.MinimumSets == 0 || direct >= target.MinimumSets;

            entries.Add(new MuscleAccountingEntryV2(
                muscle,
                direct,
                indirect,
                meaningfulDays,
                target,
                meetsTarget));
        }

        return new MuscleAccountingSummaryV2(
            TotalPhysicalWorkingSets: totalPhysicalWorkingSets,
            Muscles: entries,
            CoversKneeDominant: hasKneeDominant,
            CoversKneeFlexion: hasKneeFlexion,
            CoversHipExtension: hasHipExtension,
            CoversDirectCalves: directTotals[MuscleGroupV2.Calves] >= 2m,
            CoversDirectRearDelts: directTotals[MuscleGroupV2.RearDelts] >= 2m,
            DynamicCoreOccasions: dynamicCoreDates.Count,
            CoversDirectBiceps: directTotals[MuscleGroupV2.Biceps] >= 1m,
            CoversDirectTriceps: directTotals[MuscleGroupV2.Triceps] >= 1m,
            CoversDirectHipAbduction: directTotals[MuscleGroupV2.HipAbductors] >= 2m);
    }
}
