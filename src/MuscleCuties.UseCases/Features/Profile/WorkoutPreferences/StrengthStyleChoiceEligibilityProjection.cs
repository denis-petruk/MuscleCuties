using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

/// <summary>
/// Presentation-facing adapter for <see cref="StrengthTemplateEligibilityRule.EvaluateStyleChoice"/>.
/// It only decides what a choice surface offers; the server planner remains authoritative.
/// </summary>
public static class StrengthStyleChoiceEligibilityProjection
{
    /// <summary>
    /// True when the style may be offered as a new selection. An unknown or unavailable profile level
    /// (<paramref name="trainingExperienceLevel"/> is null) restricts nothing on the client.
    /// </summary>
    public static bool IsOffered(WorkoutActivityDto style, int? trainingExperienceLevel) =>
        trainingExperienceLevel is not { } level ||
        !Enum.IsDefined(typeof(TrainingExperienceLevel), level) ||
        !Enum.IsDefined(style) ||
        StrengthTemplateEligibilityRule.EvaluateStyleChoice(WorkoutActivityContractMapper.ToCore(style),
            (TrainingExperienceLevel)level).IsEligible;
}
