using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record CycleSafetySymptom(DateOnly Date, string Type, int Severity);
public sealed record CycleReadinessSafetyDecision(bool RequiresReview, bool RoutineSymptoms);

/// <summary>Only comparable recorded evidence can establish an unchanged symptom or pain level.</summary>
public static class CycleReadinessSafetyRule
{
    private const int NoPain = 0;
    private const int MaximumPain = 10;

    public static CycleReadinessSafetyDecision Evaluate(DateOnly today, CyclePhase phase, bool phaseReliable,
        int? todayPain, int? previousPain, IReadOnlyList<CycleSafetySymptom> symptoms)
    {
        if (todayPain is < NoPain or > MaximumPain || previousPain is < NoPain or > MaximumPain ||
            todayPain is > NoPain && (previousPain is null || todayPain > previousPain))
            return new(true, false);
        var current = symptoms.Where(x => x.Date == today).ToArray();
        foreach (var symptom in current)
        {
            if (!phaseReliable || phase != CyclePhase.Menstrual ||
                !Enum.TryParse<SymptomType>(symptom.Type, true, out var type) ||
                type is not (SymptomType.Cramps or SymptomType.Fatigue) ||
                symptom.Severity is < CycleSymptomRule.MinimumSeverity or > CycleSymptomRule.MaximumSeverity ||
                !symptoms.Any(previous => previous.Date < today &&
                    today.DayNumber - previous.Date.DayNumber <= CycleReadinessPrescriptionRule.PatternObservationWindowDays &&
                    string.Equals(previous.Type, symptom.Type, StringComparison.OrdinalIgnoreCase) &&
                    previous.Severity >= symptom.Severity && previous.Severity <= CycleSymptomRule.MaximumSeverity))
                return new(true, false);
        }
        return new(false, current.Length > 0);
    }
}
