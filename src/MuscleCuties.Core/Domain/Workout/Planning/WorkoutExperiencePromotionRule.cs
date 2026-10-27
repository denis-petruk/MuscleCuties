using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Resolves experience from the onboarding level and durable credited workout days.</summary>
public static class WorkoutExperiencePromotionRule
{
    public const int IntermediateCompletedDays = 20;
    public const int AdvancedCompletedDays = 130;
    public const int MinimumCreditedDays = 0;

    /// <param name="creditedDistinctDays">Distinct completed workout days retained independently of workout deletion. Manual workouts are excluded by the caller.</param>
    public static TrainingExperienceLevel Resolve(TrainingExperienceLevel onboardingLevel, int creditedDistinctDays)
    {
        var baseline = onboardingLevel switch
        {
            TrainingExperienceLevel.Intermediate => TrainingExperienceLevel.Intermediate,
            TrainingExperienceLevel.Advanced => TrainingExperienceLevel.Advanced,
            _ => TrainingExperienceLevel.Beginner
        };
        var days = Math.Max(MinimumCreditedDays, creditedDistinctDays);

        if (baseline == TrainingExperienceLevel.Advanced || days >= AdvancedCompletedDays)
            return TrainingExperienceLevel.Advanced;
        if (baseline == TrainingExperienceLevel.Intermediate || days >= IntermediateCompletedDays)
            return TrainingExperienceLevel.Intermediate;
        return TrainingExperienceLevel.Beginner;
    }
}
