namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityExperience
{
    Unknown,
    Beginner,
    Established,
    Returning
}

/// <summary>Evidence belongs to one activity. Self-reported evidence is explicitly identified.</summary>
public sealed record ActivityTrainingHistory(
    string ActivityId,
    int? ConsistentParticipationWeeks,
    decimal? AverageSessionsPerWeekInRecentFourWeeks,
    DateOnly? LastCompletedSessionDate,
    bool IsSelfReported);

public sealed record ActivityExperienceResult(
    ActivityExperience Experience,
    bool IsSelfReported,
    IReadOnlyList<string> Reasons);

public static class ActivityExperienceRule
{
    public const int MinimumConsistentWeeksForEstablished = 12;
    public const int EstablishedSessionsPerWeek = 2;
    public const int ReturningInactivityDays = 28;

    public static ActivityExperienceResult Classify(
        string activityId,
        ActivityTrainingHistory? history,
        DateOnly localDate)
    {
        if (string.IsNullOrWhiteSpace(activityId) || history is null ||
            !string.Equals(activityId, history.ActivityId, StringComparison.Ordinal) ||
            history.ConsistentParticipationWeeks is < 0 ||
            history.AverageSessionsPerWeekInRecentFourWeeks is < 0 ||
            history.LastCompletedSessionDate > localDate)
            return new(ActivityExperience.Unknown, history?.IsSelfReported == true,
                ["ActivityHistoryInsufficientOrInvalid"]);

        if (history.ConsistentParticipationWeeks is not { } weeks ||
            history.AverageSessionsPerWeekInRecentFourWeeks is not { } average ||
            history.LastCompletedSessionDate is not { } lastSessionDate)
        {
            return new(ActivityExperience.Unknown, history.IsSelfReported,
                ["ActivityHistoryInsufficient"]);
        }

        var isEstablished = weeks >= ActivityExperienceRule.MinimumConsistentWeeksForEstablished &&
                            average >= ActivityExperienceRule.EstablishedSessionsPerWeek;
        if (isEstablished &&
            localDate.DayNumber - lastSessionDate.DayNumber >= ActivityExperienceRule.ReturningInactivityDays)
            return new(ActivityExperience.Returning, history.IsSelfReported,
                ["EstablishedActivityAfterExtendedBreak"]);

        if (isEstablished)
            return new(ActivityExperience.Established, history.IsSelfReported,
                ["EstablishedActivityHistory"]);

        return new(ActivityExperience.Beginner, history.IsSelfReported,
            ["BelowEstablishedActivityThreshold"]);
    }
}
