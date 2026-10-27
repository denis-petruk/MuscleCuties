using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Maps activity evidence into the shared domain safety requirements.</summary>
public static class WorkoutActivityRequirements
{
    public const string SwimmingFacilityMissing = ActivitySafetyAnswersRule.SwimmingFacilityMissing;
    public const string SwimmingAbilityMissing = ActivitySafetyAnswersRule.SwimmingAbilityMissing;
    public const string SwimmingStyleMissing = ActivitySafetyAnswersRule.SwimmingStyleMissing;
    public const string ClimbingCompetenceMissing = ActivitySafetyAnswersRule.ClimbingCompetenceMissing;
    public const string ClimbingInstructionMissing = ActivitySafetyAnswersRule.ClimbingInstructionMissing;
    public const string YogaTypeMissing = ActivitySafetyAnswersRule.YogaTypeMissing;

    public static IReadOnlyList<string> Missing(IReadOnlyCollection<WorkoutActivityDto> selected,
        IReadOnlyCollection<ActivityEligibilityInputV020> evidence, DateOnly localDate)
    {
        var activities = selected.Where(activity => Enum.IsDefined(activity))
            .Select(WorkoutActivityContractMapper.ToCore).ToArray();
        var answers = evidence.Select(answer =>
        {
            var activity = WorkoutActivityContractMapper.ToCore(answer.Activity);
            var experience = answer.ActivityHistory is { } history
                ? ActivityExperienceRule.Classify(activity.ToString(),
                    new ActivityTrainingHistory(history.ActivityId, history.ConsistentParticipationWeeks,
                        history.AverageSessionsPerWeekInRecentFourWeeks, history.LastCompletedSessionDate,
                        history.IsSelfReported), localDate).Experience
                : ActivityExperience.Unknown;
            return new ActivitySafetyAnswers(activity, answer.HasSwimmingFacilityAccess,
                answer.CanSwimIndependently, answer.WantsContinuousSwimming, answer.HasRopeClimbingCompetence,
                answer.AppropriateClimbingInstructionAvailable, answer.IsDemandingYogaContent, experience);
        }).ToArray();
        return ActivitySafetyAnswersRule.Missing(activities, answers);
    }
}
