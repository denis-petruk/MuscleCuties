using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout;

public readonly record struct VolumeBudgetObservation(int MuscleGroupId, double FractionalSets);

public static class VolumeBudgetRule
{
    public static Dictionary<int, double> Resolve(
        IReadOnlyList<VolumeBudgetObservation>? rows,
        TrainingExperienceLevel experience,
        int? sessionMinutesTarget,
        IReadOnlySet<int>? tierCMuscleIds)
    {
        var modifier = experience switch
        {
            TrainingExperienceLevel.Beginner => 0.75,
            TrainingExperienceLevel.Intermediate => 1.0,
            TrainingExperienceLevel.Advanced => 1.1,
            _ => 0.75
        };
        var budget = new Dictionary<int, double>();
        if (rows is null)
            return budget;

        foreach (var row in rows)
        {
            if (row.MuscleGroupId <= 0)
                continue;
            var safeSets = double.IsFinite(row.FractionalSets) && row.FractionalSets > 0
                ? row.FractionalSets
                : 0;
            var ceiling = row.MuscleGroupId == 1 ? 26d : 22d;
            var scaled = Math.Min(safeSets * modifier, ceiling);
            if (sessionMinutesTarget is <= 35 && tierCMuscleIds?.Contains(row.MuscleGroupId) == true)
                scaled *= 0.6;
            budget[row.MuscleGroupId] = scaled;
        }

        return budget;
    }
}
