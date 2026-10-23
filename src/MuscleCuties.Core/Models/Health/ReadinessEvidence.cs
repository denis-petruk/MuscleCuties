namespace MuscleCuties.Core.Models.Health;

public sealed record ReadinessEvidence(
    DateOnly Date,
    Observation<double> SleepHours,
    Observation<double> SleepThreeDayAverage,
    Observation<int> StepsYesterday,
    Observation<int> StepsSevenDayAverage,
    Observation<int> Energy,
    Observation<int> Pain);
