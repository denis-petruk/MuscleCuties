using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Loads conservative evidence only; it does not publish or schedule a deload.</summary>
internal static class PostgresCanonicalRecoveryEvidence
{
    private const int UtcBoundaryAllowanceDays = 2;
    private const int RequiredRevision = 1;

    internal static async Task<DeloadPrescription> EvaluateAsync(PostgresDbContext context, Guid userId,
        Guid activePlanId, DateOnly today, TimeZoneInfo zone,
        IReadOnlyList<StrengthPlanPreviewSessionV020> previewSessions,
        CancellationToken cancellationToken)
    {
        var offset = ((int)today.DayOfWeek + CanonicalRecoveryEvidenceRule.DaysPerWeek - (int)DayOfWeek.Monday)
            % CanonicalRecoveryEvidenceRule.DaysPerWeek;
        var neededDays = offset + CanonicalRecoveryEvidenceRule.RequiredCompletedWeeks * CanonicalRecoveryEvidenceRule.DaysPerWeek;
        if (today.DayNumber < neededDays + UtcBoundaryAllowanceDays)
            return DeloadTriggerRule.Evaluate(null);
        var start = today.AddDays(-neededDays);
        var end = today.AddDays(-offset);
        var utcStart = DateTime.SpecifyKind(start.AddDays(-UtcBoundaryAllowanceDays).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var utcEnd = DateTime.SpecifyKind(end.AddDays(UtcBoundaryAllowanceDays).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var sessions = await context.WorkoutSessionLogs.AsNoTracking().Where(s => s.UserId == userId &&
            !s.IsDeleted && s.CompletedAt >= utcStart && s.CompletedAt < utcEnd && s.PlannedWorkoutDayId != null &&
            s.PlannedPrescriptionSnapshotJson != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var dayIds = sessions.Select(s => s.PlannedWorkoutDayId!.Value).Distinct().ToArray();
        var days = await context.WorkoutDays.AsNoTracking().Where(d => d.UserId == userId && d.WorkoutPlanId == activePlanId &&
            dayIds.Contains(d.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var dayExercises = await context.WorkoutDayExercises.AsNoTracking().Where(e => e.UserId == userId &&
            dayIds.Contains(e.WorkoutDayId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var logs = await context.WorkoutLogs.AsNoTracking().Where(l => l.UserId == userId && l.PlannedSessionId != null &&
            sessionIds.Contains(l.PlannedSessionId.Value)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var logIds = logs.Select(l => l.Id).ToArray();
        var exercises = await context.WorkoutExerciseLogs.AsNoTracking().Where(e => e.UserId == userId &&
            logIds.Contains(e.WorkoutLogId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var observations = new List<ObservedCompoundPerformance>();
        foreach (var session in sessions)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(session.CompletedAt!.Value, DateTimeKind.Utc), zone));
            if (localDate < start || localDate >= end || session.PlannedLocalDate != localDate ||
                session.PlannedTimeZoneId != zone.Id || !days.Any(d => d.Id == session.PlannedWorkoutDayId)) continue;
            var snapshot = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
            var matchingLogs = logs.Where(l => l.PlannedSessionId == session.Id && l.WorkoutDayId == session.PlannedWorkoutDayId &&
                l.Date == localDate).ToArray();
            if (snapshot is null || snapshot.DayId != session.PlannedWorkoutDayId || snapshot.Exercises is null || matchingLogs.Length != 1) continue;
            foreach (var prescribed in snapshot.Exercises.Where(e => !e.IsOptional && e.DurationSeconds is null))
            {
                var metadata = dayExercises.Where(e => e.Id == prescribed.Id && e.WorkoutDayId == snapshot.DayId &&
                    e.ExerciseId == prescribed.ExerciseId && e.PlanningTemplateVersion == StrengthTemplateCatalog.Version).ToArray();
                if (metadata.Length != 1) continue;
                var item = metadata[0];
                var matchingSlots = previewSessions.Where(s => s.TemplateId == item.PlanningTemplateId &&
                    s.TemplateVersion == item.PlanningTemplateVersion && s.SessionIndex == item.PlanningSessionIndex)
                    .SelectMany(s => s.Slots).Where(s => s.SlotId == item.PlanningSlotId && s.ExerciseId == prescribed.ExerciseId &&
                        s.PrescriptionRole == (int)StrengthPrescriptionRole.Main && s.IsRequired).ToArray();
                var templateSlot = StrengthTemplateCatalog.Templates.Where(t => t.TemplateId == item.PlanningTemplateId &&
                    t.Version == item.PlanningTemplateVersion && t.SessionIndex == item.PlanningSessionIndex)
                    .SelectMany(t => t.Slots).Where(s => s.SlotId == item.PlanningSlotId).ToArray();
                if (matchingSlots.Length != 1 || templateSlot.Length != 1 || templateSlot[0].MovementSlot is not
                    (StrengthTemplateSlotId.KneeDominant or StrengthTemplateSlotId.HipDominant or
                     StrengthTemplateSlotId.UpperPush or StrengthTemplateSlotId.UpperPull)) continue;
                var matchingExercises = exercises.Where(e => e.WorkoutLogId == matchingLogs[0].Id &&
                    e.WorkoutDayExerciseId == prescribed.Id && e.ExerciseId == prescribed.ExerciseId).ToArray();
                if (matchingExercises.Length != 1) continue;
                observations.Add(new(localDate, prescribed.ExerciseId, prescribed.Sets, prescribed.Reps,
                    matchingExercises[0].CompletedSets, matchingExercises[0].CompletedReps));
            }
        }
        var checks = await context.DailyReadinessLogs.AsNoTracking().Where(r => r.UserId == userId && r.Date >= start &&
            r.Date < end && r.PlanningTimeZoneId == zone.Id && r.PlanningRevision >= RequiredRevision &&
            r.PlanningRecordedAtUtc != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        var recovery = checks.Where(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(r.PlanningRecordedAtUtc!.Value, DateTimeKind.Utc), zone)) == r.Date)
            .Select(r => new ObservedRecovery(r.Date, r.PlanningSleepQuality, r.Energy, r.PlanningSoreness)).ToArray();
        return CanonicalRecoveryEvidenceRule.Evaluate(today, observations, recovery);
    }
}
