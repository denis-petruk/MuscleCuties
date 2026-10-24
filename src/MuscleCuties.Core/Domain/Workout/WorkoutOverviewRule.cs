namespace MuscleCuties.Core.Domain.Workout;

public static class WorkoutOverviewRule
{
    public static bool IsValidSessionOptionality(
        int workoutDayId,
        bool isOptional,
        bool isEasierAlternative) =>
        WorkoutSessionActionRule.IsValidIdentifier(workoutDayId) &&
        (!isEasierAlternative || isOptional);

    public static bool IsValidReadinessAdjustmentRequest(DateOnly localDate, string? timeZoneId) =>
        WorkoutScheduleMathRule.IsSupportedDate(localDate) && !string.IsNullOrWhiteSpace(timeZoneId);

    public static bool IsValidReadinessAdjustmentDecision(
        Guid proposalId,
        DateOnly localDate,
        string? timeZoneId) =>
        proposalId != Guid.Empty && IsValidReadinessAdjustmentRequest(localDate, timeZoneId);
}
