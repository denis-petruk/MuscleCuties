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
    DateTime SyncedAt,
    int? YesterdaySteps = null,
    double? LastNightSleepHours = null,
    double? AverageActiveEnergyKcal = null,
    double? AverageSleep3dHours = null)
{
    public bool HasMovementData => AverageSteps > 0;
    public bool HasSleepData => AverageSleepHours > 0 || LastNightSleepHours is > 0 || SleepQualityScore > 0;

    public string MovementSummary =>
        HasMovementData ? $"{AverageSteps:N0} avg steps" : "Steps not connected";

    public string SleepSummary =>
        AverageSleepHours > 0 ? $"{AverageSleepHours:N1}h avg sleep"
        : LastNightSleepHours is > 0 ? $"{LastNightSleepHours.Value:N1}h last night"
        : "Sleep not connected";
}
