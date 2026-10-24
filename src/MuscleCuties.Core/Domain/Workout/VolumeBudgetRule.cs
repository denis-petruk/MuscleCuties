using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout;

public readonly record struct VolumeBudgetObservation(int MuscleGroupId, double FractionalSets);

public static class VolumeBudgetRule
{
    public const double BeginnerExperienceModifier = 0.75;
    public const double IntermediateExperienceModifier = 1.0;
    public const double AdvancedExperienceModifier = 1.1;
    public const double DefaultExperienceModifier = BeginnerExperienceModifier;

    public const int MinimumValidMuscleGroupId = 1;
    public const int PrimaryMuscleGroupId = 1;

    public const double NoFractionalSets = 0.0;
    public const double PrimaryMuscleWeeklyCeilingSets = 26.0;
    public const double SecondaryMuscleWeeklyCeilingSets = 22.0;

    public const int ShortSessionMinutesThreshold = 35;
    public const double TierCMuscleShortSessionReductionMultiplier = 0.6;

    public static Dictionary<int, double> Resolve(
        IReadOnlyList<VolumeBudgetObservation>? rows,
        TrainingExperienceLevel experience,
        int? sessionMinutesTarget,
        IReadOnlySet<int>? tierCMuscleIds)
    {
        var modifier = experience switch
        {
            TrainingExperienceLevel.Beginner => BeginnerExperienceModifier,
            TrainingExperienceLevel.Intermediate => IntermediateExperienceModifier,
            TrainingExperienceLevel.Advanced => AdvancedExperienceModifier,
            _ => DefaultExperienceModifier
        };
        var budget = new Dictionary<int, double>();
        if (rows is null)
            return budget;

        foreach (var row in rows)
        {
            if (row.MuscleGroupId < MinimumValidMuscleGroupId)
                continue;
            var safeSets = double.IsFinite(row.FractionalSets) && row.FractionalSets > NoFractionalSets
                ? row.FractionalSets
                : NoFractionalSets;
            var ceiling = row.MuscleGroupId == PrimaryMuscleGroupId
                ? PrimaryMuscleWeeklyCeilingSets
                : SecondaryMuscleWeeklyCeilingSets;
            var scaled = Math.Min(safeSets * modifier, ceiling);
            if (sessionMinutesTarget is <= ShortSessionMinutesThreshold &&
                tierCMuscleIds?.Contains(row.MuscleGroupId) == true)
                scaled *= TierCMuscleShortSessionReductionMultiplier;
            budget[row.MuscleGroupId] = scaled;
        }

        return budget;
    }
}
