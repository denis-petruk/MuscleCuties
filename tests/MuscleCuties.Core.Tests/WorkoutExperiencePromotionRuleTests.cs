using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Tests;

public class WorkoutExperiencePromotionRuleTests
{
    [Theory]
    [InlineData(TrainingExperienceLevel.Beginner, 19, TrainingExperienceLevel.Beginner)]
    [InlineData(TrainingExperienceLevel.Beginner, 20, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Beginner, 129, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Beginner, 130, TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Intermediate, 20, TrainingExperienceLevel.Intermediate)]
    [InlineData(TrainingExperienceLevel.Intermediate, 130, TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Advanced, 0, TrainingExperienceLevel.Advanced)]
    [InlineData(TrainingExperienceLevel.Unknown, -1, TrainingExperienceLevel.Beginner)]
    public void Resolve_uses_onboarding_baseline_and_credited_day_thresholds(
        TrainingExperienceLevel baseline, int days, TrainingExperienceLevel expected)
    {
        Assert.Equal(expected, WorkoutExperiencePromotionRule.Resolve(baseline, days));
    }
}
