namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityExperienceV020
{
    Unknown,
    Beginner,
    Established,
    Returning
}

/// <summary>Evidence belongs to one activity. Self-reported evidence is explicitly identified.</summary>
public sealed record ActivityTrainingHistoryV020(
    string ActivityId,
    int? ConsistentParticipationWeeks,
    decimal? AverageSessionsPerWeekInRecentFourWeeks,
    DateOnly? LastCompletedSessionDate,
    bool IsSelfReported);

public sealed record ActivityExperienceResultV020(
    ActivityExperienceV020 Experience,
    bool IsSelfReported,
    IReadOnlyList<string> Reasons);

public static class ActivityExperienceRuleV020
{
    public static ActivityExperienceResultV020 Classify(
        string activityId,
        ActivityTrainingHistoryV020? history,
        DateOnly localDate)
    {
        if (string.IsNullOrWhiteSpace(activityId) || history is null ||
            !string.Equals(activityId, history.ActivityId, StringComparison.Ordinal) ||
            history.ConsistentParticipationWeeks is < 0 ||
            history.AverageSessionsPerWeekInRecentFourWeeks is < 0 ||
            history.LastCompletedSessionDate > localDate)
            return new(ActivityExperienceV020.Unknown, history?.IsSelfReported == true,
                ["ActivityHistoryInsufficientOrInvalid"]);

        if (history.LastCompletedSessionDate is { } last &&
            localDate.DayNumber - last.DayNumber >= WorkoutTrainingPolicyV020.ReturningInactivityDays &&
            history.ConsistentParticipationWeeks >= WorkoutTrainingPolicyV020.MinimumConsistentWeeksForEstablished)
            return new(ActivityExperienceV020.Returning, history.IsSelfReported,
                ["EstablishedActivityAfterExtendedBreak"]);

        if (history.ConsistentParticipationWeeks is { } weeks &&
            history.AverageSessionsPerWeekInRecentFourWeeks is { } average &&
            weeks >= WorkoutTrainingPolicyV020.MinimumConsistentWeeksForEstablished &&
            average >= WorkoutTrainingPolicyV020.EstablishedSessionsPerWeek)
            return new(ActivityExperienceV020.Established, history.IsSelfReported,
                ["EstablishedActivityHistory"]);

        if (history.ConsistentParticipationWeeks is { } knownWeeks &&
            knownWeeks < WorkoutTrainingPolicyV020.MinimumConsistentWeeksForEstablished)
            return new(ActivityExperienceV020.Beginner, history.IsSelfReported,
                ["FewerThanTwelveConsistentWeeks"]);

        return new(ActivityExperienceV020.Unknown, history.IsSelfReported,
            ["ActivityHistoryInsufficient"]);
    }
}
