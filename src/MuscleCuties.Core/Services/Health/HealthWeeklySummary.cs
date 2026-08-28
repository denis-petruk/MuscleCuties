namespace MuscleCuties.Core.Services.Health;

public sealed record HealthWeeklySummary(
    HealthDataSource Source,
    DateTime WeekStart,
    DateTime WeekEnd,
    int AverageSteps,
    double AverageSleepHours,
    int SleepQualityScore,
    int RestingHeartRate,
    int HrvScore,
    DateTime SyncedAt)
{
    public bool HasMovementData => AverageSteps > 0;
    public bool HasSleepData => AverageSleepHours > 0 || SleepQualityScore > 0;

    public string MovementSummary =>
        HasMovementData ? $"{AverageSteps:N0} avg steps" : "Steps not connected";

    public string SleepSummary =>
        HasSleepData ? $"{AverageSleepHours:N1}h avg sleep" : "Sleep not connected";
}
