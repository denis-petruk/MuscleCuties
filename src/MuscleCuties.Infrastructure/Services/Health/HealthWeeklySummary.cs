namespace MuscleCuties.Core.Services.Health;

/// <summary>A week of health data, limited to steps, sleep, active energy, resting heart rate and HRV.</summary>
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
    /// <summary>Whether the week holds any movement, so the card can say so instead of showing zeroes.</summary>
    public bool HasMovementData => AverageSteps > 0;
    /// <summary>Whether the week holds any sleep, judged from either the average or the latest night.</summary>
    public bool HasSleepData => AverageSleepHours > 0 || LastNightSleepHours is > 0 || SleepQualityScore > 0;

    /// <summary>The movement line for the card, or the wording for a week without any.</summary>
    public string MovementSummary =>
        HasMovementData ? $"{AverageSteps:N0} avg steps" : "Steps not connected";

    /// <summary>The sleep line for the card, or the wording for a week without any.</summary>
    public string SleepSummary =>
        AverageSleepHours > 0 ? $"{AverageSleepHours:N1}h avg sleep"
        : LastNightSleepHours is > 0 ? $"{LastNightSleepHours.Value:N1}h last night"
        : "Sleep not connected";
}
