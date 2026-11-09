using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.Core.Tests;

/// <summary>C1/OD-7: the choice-surface projection of the reviewed HVS/SHI template gates.</summary>
public sealed class StrengthStyleChoiceEligibilityTests
{
    [Theory]
    [InlineData(WorkoutActivityType.HighVolumeStrength, TrainingExperienceLevel.Beginner, false, StrengthTemplateEligibilityRule.HvsProfileLevelTooLow)]
    [InlineData(WorkoutActivityType.StrengthHighIntensity, TrainingExperienceLevel.Beginner, false, StrengthTemplateEligibilityRule.ShiProfileLevelTooLow)]
    [InlineData(WorkoutActivityType.GeneralStrength, TrainingExperienceLevel.Beginner, true, null)]
    [InlineData(WorkoutActivityType.HighVolumeStrength, TrainingExperienceLevel.Intermediate, true, null)]
    [InlineData(WorkoutActivityType.StrengthHighIntensity, TrainingExperienceLevel.Advanced, true, null)]
    [InlineData(WorkoutActivityType.Running, TrainingExperienceLevel.Beginner, true, null)]
    public void BeginnersAreNotOfferedStylesRestrictedToEstablishedHistory(
        WorkoutActivityType style, TrainingExperienceLevel level, bool eligible, string? reason)
    {
        var result = StrengthTemplateEligibilityRule.EvaluateStyleChoice(style, level);

        Assert.Equal(eligible, result.IsEligible);
        Assert.Equal(reason, result.ReasonCode);
    }

    [Theory]
    [InlineData(WorkoutActivityDto.HighVolumeStrength, 1, false)]
    [InlineData(WorkoutActivityDto.StrengthHighIntensity, 1, false)]
    [InlineData(WorkoutActivityDto.GeneralStrength, 1, true)]
    [InlineData(WorkoutActivityDto.HighVolumeStrength, 2, true)]
    [InlineData(WorkoutActivityDto.HighVolumeStrength, null, true)] // profile unavailable: server decides
    [InlineData(WorkoutActivityDto.HighVolumeStrength, 99, true)] // undefined level: no client restriction
    public void ProjectionOnlyRestrictsAKnownBeginnerProfile(WorkoutActivityDto style, int? level, bool offered) =>
        Assert.Equal(offered, StrengthStyleChoiceEligibilityProjection.IsOffered(style, level));
}
