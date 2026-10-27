using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record ActivitySafetyAnswers(WorkoutActivityType Activity, bool? SwimmingFacility,
    bool? SwimmingAbility, bool? SwimmingContinuous, bool? ClimbingCompetence,
    bool? ClimbingInstruction, bool? DemandingYoga, ActivityExperience ClimbingExperience = ActivityExperience.Unknown);

public enum ActivitySafetyAnswerState { Unknown, Safe, Unsafe }

public static class ActivitySafetyAnswersRule
{
    public const string SwimmingFacilityMissing = "SwimmingFacilityMissing";
    public const string SwimmingAbilityMissing = "SwimmingAbilityMissing";
    public const string SwimmingStyleMissing = "SwimmingStyleMissing";
    public const string ClimbingCompetenceMissing = "ClimbingCompetenceMissing";
    public const string ClimbingInstructionMissing = "ClimbingInstructionMissing";
    public const string YogaTypeMissing = "YogaTypeMissing";
    public const string DuplicateAnswers = "DuplicateActivityAnswers";

    // Classification uses only explicit safety facts; Unknown must never restore an activity.
    public static ActivitySafetyAnswerState Classify(ActivitySafetyAnswers? answer) => answer?.Activity switch
    {
        WorkoutActivityType.Swimming when answer.SwimmingFacility is false || answer.SwimmingAbility is false => ActivitySafetyAnswerState.Unsafe,
        WorkoutActivityType.Swimming when answer.SwimmingFacility is true && answer.SwimmingAbility is true => ActivitySafetyAnswerState.Safe,
        WorkoutActivityType.RockClimbing when answer.ClimbingCompetence is false => ActivitySafetyAnswerState.Unsafe,
        WorkoutActivityType.RockClimbing when answer.ClimbingCompetence is true &&
            (answer.ClimbingExperience == ActivityExperience.Established || answer.ClimbingInstruction is true) => ActivitySafetyAnswerState.Safe,
        _ => ActivitySafetyAnswerState.Unknown
    };

    public static IReadOnlyList<string> Missing(IReadOnlyCollection<WorkoutActivityType> selected,
        IReadOnlyCollection<ActivitySafetyAnswers> answers)
    {
        var missing = new List<string>();
        if (answers.GroupBy(answer => answer.Activity).Any(group => group.Count() > 1))
            missing.Add(DuplicateAnswers);
        var swimming = answers.FirstOrDefault(answer => answer.Activity == WorkoutActivityType.Swimming);
        if (selected.Contains(WorkoutActivityType.Swimming))
        {
            if (swimming?.SwimmingFacility is null) missing.Add(SwimmingFacilityMissing);
            if (swimming?.SwimmingFacility is true && swimming.SwimmingAbility is null) missing.Add(SwimmingAbilityMissing);
            if (swimming?.SwimmingFacility is true && swimming.SwimmingAbility is true && swimming.SwimmingContinuous is null)
                missing.Add(SwimmingStyleMissing);
        }
        var climbing = answers.FirstOrDefault(answer => answer.Activity == WorkoutActivityType.RockClimbing);
        if (selected.Contains(WorkoutActivityType.RockClimbing))
        {
            if (climbing?.ClimbingCompetence is null) missing.Add(ClimbingCompetenceMissing);
            if (climbing?.ClimbingCompetence is true && climbing.ClimbingExperience != ActivityExperience.Established &&
                climbing.ClimbingInstruction is null) missing.Add(ClimbingInstructionMissing);
        }
        if (selected.Contains(WorkoutActivityType.Yoga) &&
            answers.FirstOrDefault(answer => answer.Activity == WorkoutActivityType.Yoga)?.DemandingYoga is null)
            missing.Add(YogaTypeMissing);
        return missing;
    }
}
