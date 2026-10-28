using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthScheduleAssignment(int SessionIndex, DayOfWeek Day);
public sealed record StrengthScheduleAssignmentResult(
    IReadOnlyList<StrengthScheduleAssignment> Assignments, IReadOnlyList<string> Limitations);

/// <summary>Finds the largest safe subset of reviewed strength sessions alongside fixed activities.</summary>
public static class StrengthScheduleAssignmentRule
{
    public const string SessionsReducedForMuscleRecovery = "StrengthSessionsReducedForMuscleRecovery";

    public static MixedActivityWeekScheduleResult Reconcile(
        MixedActivityWeekScheduleResult schedule,
        IReadOnlyList<IReadOnlySet<string>?> muscles,
        IReadOnlyCollection<DayOfWeek> availableDays,
        WorkoutActivityType strengthActivity)
    {
        var existing = schedule.Sessions.Where(session => session.Activity == strengthActivity).ToArray();
        if (existing.Length == 0) return schedule;
        var other = schedule.Sessions.Where(session => session.Activity != strengthActivity).ToArray();
        var originals = schedule.Activities.Where(decision => decision.Prescription is not null)
            .GroupBy(decision => decision.EffectiveActivity ?? decision.Activity)
            .ToDictionary(group => group.Key, group => group.First().Prescription);
        var otherValidation = other.Select(session => new WorkoutScheduleValidationSession(
            session.Day, session.Activity, session.IsHardSession, null, session.DurationMinutes,
            originals.GetValueOrDefault(session.Activity), session.OccurrencePrescription)).ToArray();
        var assignment = Assign(muscles, availableDays, otherValidation, strengthActivity,
            existing[0].IsHardSession, existing.Length, originals.GetValueOrDefault(strengthActivity), existing[0].DurationMinutes);
        if (assignment.Limitations.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing))
            return schedule with { InputReasons = schedule.InputReasons.Concat(assignment.Limitations).Distinct().ToArray() };
        var sessions = MixedActivityWeekScheduleRule.ApplyOccurrenceAdjustments(other.Concat(assignment.Assignments.Select(item =>
            existing[0] with { Day = item.Day })), originals);
        var activities = schedule.Activities.Select(decision => decision.Activity == strengthActivity
            ? decision with
            {
                ScheduledSessionsPerWeek = assignment.Assignments.Count,
                Disposition = assignment.Assignments.Count == 0 ? MixedActivityScheduleDisposition.Omitted :
                    assignment.Limitations.Count > 0 ? MixedActivityScheduleDisposition.PartiallyScheduled : decision.Disposition,
                Reasons = decision.Reasons.Concat(assignment.Limitations).Distinct().ToArray()
            } : decision).ToArray();
        return schedule with { Sessions = sessions, Activities = activities, StrengthAssignments = assignment.Assignments };
    }
    public static StrengthScheduleAssignmentResult Assign(
        IReadOnlyList<IReadOnlySet<string>?> muscles,
        IReadOnlyCollection<DayOfWeek> availableDays,
        IReadOnlyList<WorkoutScheduleValidationSession> otherSessions,
        WorkoutActivityType strengthActivity,
        bool isHard,
        int maximumSessions,
        ActivitySessionPrescription? strengthPrescription = null,
        decimal? strengthDuration = null)
    {
        if (muscles.Any(groups => groups is null || groups.Count == 0 || groups.Any(string.IsNullOrWhiteSpace)))
            return new([], [WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing]);

        var days = availableDays.Distinct().OrderBy(day => day).ToArray();
        var best = new List<StrengthScheduleAssignment>();
        var current = new List<StrengthScheduleAssignment>();
        Search(0);
        return new(best, best.Count < Math.Min(maximumSessions, muscles.Count)
            ? [SessionsReducedForMuscleRecovery] : []);

        void Search(int index)
        {
            if (current.Count > best.Count)
                best = [.. current];
            if (index >= muscles.Count || current.Count >= maximumSessions)
                return;

            foreach (var day in days)
            {
                if (current.Any(item => item.Day == day)) continue;
                var candidate = otherSessions.Concat(current.Select(item =>
                    new WorkoutScheduleValidationSession(item.Day, strengthActivity, isHard, muscles[item.SessionIndex], strengthDuration, strengthPrescription)))
                    .Append(new WorkoutScheduleValidationSession(day, strengthActivity, isHard, muscles[index], strengthDuration, strengthPrescription))
                    .ToArray();
                if (WorkoutScheduleValidationRule.Validate(AdjustCandidate(candidate)).Count != 0) continue;
                current.Add(new(index, day));
                Search(index + 1);
                current.RemoveAt(current.Count - 1);
            }
            Search(index + 1);
        }
    }

    private static WorkoutScheduleValidationSession[] AdjustCandidate(WorkoutScheduleValidationSession[] source)
    {
        var result = source.Select(session => session with
        {
            DurationMinutes = session.OccurrencePrescription is not null && session.OriginalPrescription is not null
                ? SameDayDemandAdjustmentRule.TotalMinutes(session.OriginalPrescription) : session.DurationMinutes,
            OccurrencePrescription = null
        }).ToArray();
        foreach (var day in result.Where(session => session.IsHard).GroupBy(session => session.Day))
        {
            if (day.Count() != MixedActivityWeekScheduleRule.MaximumSessionsPerDay) continue;
            foreach (var session in day)
            {
                var adjusted = SameDayDemandAdjustmentRule.Adjust(session.OriginalPrescription);
                if (adjusted is null) continue;
                result[Array.IndexOf(result, session)] = session with
                {
                    OccurrencePrescription = adjusted,
                    DurationMinutes = SameDayDemandAdjustmentRule.TotalMinutes(adjusted)
                };
                break;
            }
        }
        return result;
    }
}
