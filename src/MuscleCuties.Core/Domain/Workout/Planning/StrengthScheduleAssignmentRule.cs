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
        var otherValidation = other.Select(session => new WorkoutScheduleValidationSession(
            session.Day, session.Activity, session.IsHardSession)).ToArray();
        var assignment = Assign(muscles, availableDays, otherValidation, strengthActivity,
            existing[0].IsHardSession, existing.Length);
        if (assignment.Limitations.Contains(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing))
            return schedule with { InputReasons = schedule.InputReasons.Concat(assignment.Limitations).Distinct().ToArray() };
        var sessions = other.Concat(assignment.Assignments.Select(item =>
            existing[0] with { Day = item.Day })).ToArray();
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
        int maximumSessions)
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
                    new WorkoutScheduleValidationSession(item.Day, strengthActivity, isHard, muscles[item.SessionIndex])))
                    .Append(new WorkoutScheduleValidationSession(day, strengthActivity, isHard, muscles[index]))
                    .ToArray();
                if (WorkoutScheduleValidationRule.Validate(candidate).Count != 0) continue;
                current.Add(new(index, day));
                Search(index + 1);
                current.RemoveAt(current.Count - 1);
            }
            Search(index + 1);
        }
    }
}
