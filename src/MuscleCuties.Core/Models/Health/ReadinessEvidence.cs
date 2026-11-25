namespace MuscleCuties.Core.Models.Health;

/// <summary>The signals the client sends when the device holds no history, kept to the metrics the app reads.</summary>
public sealed record ReadinessEvidence(
    DateOnly Date,
    Observation<double> SleepHours,
    Observation<double> SleepThreeDayAverage,
    Observation<int> StepsYesterday,
    Observation<int> StepsSevenDayAverage,
    Observation<int> Energy,
    Observation<int> Pain);
