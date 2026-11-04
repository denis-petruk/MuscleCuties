namespace MuscleCuties.Core.Domain.Workout.Planning;

// Inputs must come from completed planned sessions with immutable prescriptions and dated check-ins.
public sealed record ObservedCompoundPerformance(DateOnly LocalDate, int ExerciseId,
    int PrescribedSets, int PrescribedRepetitions, int CompletedSets, int CompletedRepetitions);
public sealed record ObservedRecovery(DateOnly LocalDate, int? SleepQuality, int? Energy, int? Soreness);

public static class CanonicalRecoveryEvidenceRule
{
    public const int DaysPerWeek = 7;
    public const int RequiredCompletedWeeks = 2;
    public const int MinimumExerciseId = 1;

    /// <summary>Only two complete preceding calendar weeks can support the legacy deload rule.</summary>
    public static DeloadPrescription Evaluate(DateOnly today, IReadOnlyList<ObservedCompoundPerformance>? performances,
        IReadOnlyList<ObservedRecovery>? recovery)
    {
        var mondayOffset = ((int)today.DayOfWeek + DaysPerWeek - (int)DayOfWeek.Monday) % DaysPerWeek;
        if (today.DayNumber < mondayOffset + RequiredCompletedWeeks * DaysPerWeek)
            return DeloadTriggerRule.Evaluate(null);
        var currentWeek = today.AddDays(-mondayOffset);
        var firstWeek = currentWeek.AddDays(-RequiredCompletedWeeks * DaysPerWeek);
        var observations = (performances ?? []).Where(p => p.LocalDate >= firstWeek && p.LocalDate < currentWeek &&
            p.ExerciseId >= MinimumExerciseId && p.PrescribedSets > 0 && p.PrescribedRepetitions > 0 &&
            p.CompletedSets >= p.PrescribedSets && p.CompletedRepetitions > 0).ToArray();
        var commonExercises = observations.Where(p => p.LocalDate < firstWeek.AddDays(DaysPerWeek))
            .Select(p => p.ExerciseId).Intersect(observations.Where(p => p.LocalDate >= firstWeek.AddDays(DaysPerWeek))
                .Select(p => p.ExerciseId)).ToHashSet();
        var weeks = new List<WeeklyPerformanceAndRecoveryRecord>();
        for (var index = 0; index < RequiredCompletedWeeks; index++)
        {
            var start = firstWeek.AddDays(index * DaysPerWeek);
            var end = start.AddDays(DaysPerWeek);
            var weekPerformance = observations.Where(p => p.LocalDate >= start && p.LocalDate < end &&
                commonExercises.Contains(p.ExerciseId)).ToArray();
            var checks = (recovery ?? []).Where(r => r.LocalDate >= start && r.LocalDate < end).ToArray();
            var sleep = AverageValid(checks.Select(r => r.SleepQuality));
            var energy = AverageValid(checks.Select(r => r.Energy));
            var soreness = AverageValid(checks.Select(r => r.Soreness));
            weeks.Add(new WeeklyPerformanceAndRecoveryRecord(index,
                weekPerformance.Length > 0 && weekPerformance.All(p => p.CompletedRepetitions < p.PrescribedRepetitions),
                sleep, energy, soreness));
        }
        return DeloadTriggerRule.Evaluate(weeks);
    }

    private static decimal? AverageValid(IEnumerable<int?> values)
    {
        var valid = values.Where(v => v is >= TodayCheckInScoreRule.ReadinessScaleMinimum and
            <= TodayCheckInScoreRule.ReadinessScaleMaximum).Select(v => (decimal)v!.Value).ToArray();
        return valid.Length == 0 ? null : valid.Average();
    }
}
