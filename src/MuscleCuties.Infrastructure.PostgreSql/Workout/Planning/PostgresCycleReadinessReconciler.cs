using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Recomputes same-day overlays while preserving accepted plan prescriptions.</summary>
internal static class PostgresCycleReadinessReconciler
{
    private const int MinimumWorkingSets = 1;
    private const int NoOptionalSets = 0;
    private const int SingleSetReduction = 1;
    // The caller owns the tenant transaction and workout account lock.
    public static async Task MarkPendingAsync(PostgresDbContext db, Guid userId, CancellationToken ct)
    {
        var state = await db.WorkoutAccountPlanningStates.SingleOrDefaultAsync(x => x.UserId == userId, ct).ConfigureAwait(false);
        if (state is not null) state.CycleReadinessPending = true;
    }

    public static async Task<bool> ReconcileAsync(PostgresDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var state = await db.WorkoutAccountPlanningStates.SingleOrDefaultAsync(x => x.UserId == userId, ct).ConfigureAwait(false);
        if (state?.TimeZoneId is not { } zoneId) return false;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
        if (await WorkoutSessionAccountLock.HasActivePreviewAsync(db, userId, now.UtcDateTime, ct).ConfigureAwait(false) ||
            await db.WorkoutSessionLogs.AsNoTracking().AnyAsync(x => x.UserId == userId && !x.IsDeleted && x.CompletedAt == null, ct).ConfigureAwait(false))
            return false;
        var today = WorkoutAccountCalendarRule.LocalDate(now, zone);
        var days = await db.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.Date == today &&
            !day.IsRemovedBySafety && !day.IsRemovedByReadiness && !day.IsRemovedByUserAdjustment &&
            db.WorkoutPlans.Any(plan => plan.UserId == userId && plan.Id == day.WorkoutPlanId && plan.IsActive))
            .ToListAsync(ct).ConfigureAwait(false);
        var completedDayIds = await db.WorkoutSessionLogs.AsNoTracking().Where(session => session.UserId == userId &&
            !session.IsDeleted && session.CompletedAt != null && session.PlannedWorkoutDayId != null)
            .Select(session => session.PlannedWorkoutDayId!.Value).ToListAsync(ct).ConfigureAwait(false);
        var deferredSafety = false;
        foreach (var day in days.Where(day => !completedDayIds.Contains(day.Id)))
        {
            if (state.SafetyRegenerationPending && (day.ActivityTypeId is not { } activity ||
                !Enum.IsDefined((WorkoutActivityType)activity) || !StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)activity)))
            { deferredSafety = true; continue; }
            var exercises = await db.WorkoutDayExercises.AsNoTracking().Where(x => x.UserId == userId &&
                x.WorkoutDayId == day.Id && !x.IsRemovedByReadiness).OrderBy(x => x.Id).ToListAsync(ct).ConfigureAwait(false);
            var baseline = new PlannedPrescriptionSnapshot(day.Id, day.Name, day.WorkoutType, day.ActivityTypeId,
                day.PlannedMainDurationMinutes, exercises.Select(PlannedExerciseSnapshot.FromRecord).ToArray())
                { ActivityPrescriptionJson = day.PlannedActivityPrescriptionJson };
            var result = await PostgresCycleReadinessPrescription.ApplyAsync(db, userId, today, zoneId, now, baseline, ct).ConfigureAwait(false);
            var checkIn = await db.DailyReadinessLogs.AsNoTracking().Where(x => x.UserId == userId && x.Date == today)
                .OrderByDescending(x => x.PlanningRevision).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var phase = await db.CyclePhaseLogs.AsNoTracking().Where(x => x.UserId == userId && x.LocalDate == today)
                .OrderByDescending(x => x.LoggedAt).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var cycle = phase?.CycleLogId is { } cycleId
                ? await db.CycleLogs.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Id == cycleId, ct).ConfigureAwait(false)
                : null;
            var anchor = cycle is not null && await db.CyclePhaseLogs.AsNoTracking().AnyAsync(x => x.UserId == userId &&
                x.CycleLogId == cycle.Id && x.LocalDate == cycle.StartDate && x.Phase == "Menstrual", ct).ConfigureAwait(false);
            var profile = await db.UserProfiles.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.PhaseBaselinesJson, x.ReproductiveContextJson }).SingleOrDefaultAsync(ct).ConfigureAwait(false);
            var injuries = await db.WorkoutInjuryLogs.AsNoTracking().Where(x => x.UserId == userId && x.Status != "Cleared")
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.Status }).ToArrayAsync(ct).ConfigureAwait(false);
            var fromDate = today.AddDays(-CycleReadinessPrescriptionRule.PatternObservationWindowDays);
            var symptoms = await db.SymptomLogs.AsNoTracking().Where(x => x.UserId == userId && x.Date >= fromDate && x.Date <= today)
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.Date, x.Severity, x.SymptomType }).ToArrayAsync(ct).ConfigureAwait(false);
            var previousPain = await db.DailyReadinessLogs.AsNoTracking().Where(x => x.UserId == userId &&
                x.Date >= fromDate && x.Date < today && x.Pain != null).OrderByDescending(x => x.Date)
                .Select(x => new { x.Date, x.Pain }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var history = await PostgresCyclePersonalizationEvidence.LoadAsync(db, userId, today, zoneId, ct).ConfigureAwait(false);
            var revisionId = await db.WorkoutPlanRevisionsV02.AsNoTracking().Where(x => x.UserId == userId && x.PlanId == day.WorkoutPlanId)
                .OrderByDescending(x => x.RevisionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var fingerprint = PlanningInputFingerprint.Hash(new { PlanId = day.WorkoutPlanId, RevisionId = revisionId,
                PolicyVersion = CycleReadinessPrescriptionRule.PolicyVersion, Baseline = baseline, CheckIn = checkIn, Phase = phase, Cycle = cycle, Anchor = anchor,
                Profile = profile, Injuries = injuries, Symptoms = symptoms, PreviousPain = previousPain,
                state.CycleReadinessPainReviewDate, History = history, Zone = zoneId });
            var row = await db.WorkoutCycleReadinessOccurrences.SingleOrDefaultAsync(x => x.UserId == userId && x.WorkoutDayId == day.Id, ct).ConfigureAwait(false);
            if (row is null)
            {
                row = new WorkoutCycleReadinessOccurrenceRecord { Id = Guid.NewGuid(), UserId = userId, WorkoutDayId = day.Id };
                db.WorkoutCycleReadinessOccurrences.Add(row);
            }
            if (row.InputFingerprint != fingerprint || row.PrescriptionJson != result.Serialize())
            {
                row.LocalDate = today; row.InputFingerprint = fingerprint;
                row.PrescriptionJson = result.Serialize(); row.UpdatedAtUtc = now.UtcDateTime;
            }
        }
        state.CycleReadinessPending = deferredSafety;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public static bool MatchesBaseline(PlannedPrescriptionSnapshot baseline, PlannedPrescriptionSnapshot? adjusted)
    {
        if (adjusted is null || adjusted.DayId != baseline.DayId || adjusted.Name != baseline.Name ||
            adjusted.WorkoutType != baseline.WorkoutType || adjusted.ActivityTypeId != baseline.ActivityTypeId ||
            adjusted.PlannedMainDurationMinutes != baseline.PlannedMainDurationMinutes ||
            adjusted.ActivityPrescriptionJson != baseline.ActivityPrescriptionJson ||
            adjusted.Exercises is null || adjusted.Exercises.Count != baseline.Exercises.Count)
            return false;
        var original = baseline.Exercises.ToDictionary(x => x.Id);
        if (original.Count != adjusted.Exercises.Select(x => x.Id).Distinct().Count()) return false;
        if (adjusted.DoseAdjustmentPolicyVersion != CycleReadinessPrescriptionRule.PolicyVersion) return false;
        var valid = adjusted.Exercises.All(x => original.TryGetValue(x.Id, out var source) &&
            x.ExerciseId == source.ExerciseId && x.Sets >= MinimumWorkingSets && x.Sets <= source.Sets &&
            x.OptionalSets >= NoOptionalSets && x.OptionalSets < x.Sets && x.OptionalSets <= source.OptionalSets &&
            x.Reps == source.Reps && x.DurationSeconds == source.DurationSeconds && x.IsOptional == source.IsOptional &&
            x.TargetRepetitionsInReserve == source.TargetRepetitionsInReserve && x.PlanningSlotId == source.PlanningSlotId);
        if (!valid) return false;
        var changed = adjusted.Exercises.Where(x => x.Sets != original[x.Id].Sets || x.OptionalSets != original[x.Id].OptionalSets).ToArray();
        if (changed.Length == NoOptionalSets)
            return adjusted.DoseAdjustmentReasonCode is CycleReadinessPrescriptionRule.NoAdjustment or CycleReadinessPrescriptionRule.SafetyReviewRequired;
        if (changed.Length != SingleSetReduction) return false;
        var item = changed[0];
        var before = original[item.Id];
        return item.IsOptional && !string.IsNullOrWhiteSpace(before.PlanningSlotId) && before.Sets - item.Sets == SingleSetReduction &&
            item.OptionalSets == Math.Min(before.OptionalSets, item.Sets - MinimumWorkingSets) &&
            ((adjusted.DoseAdjustmentReasonCode, adjusted.DoseAdjustmentConfidence) switch
            {
                (CycleReadinessPrescriptionRule.DailyReadinessAdjustment, nameof(CycleReadinessConfidence.Current)) => true,
                (CycleReadinessPrescriptionRule.ProvisionalPhaseAdjustment, nameof(CycleReadinessConfidence.Provisional)) => true,
                (CycleReadinessPrescriptionRule.RepeatedPhaseAdjustment, nameof(CycleReadinessConfidence.Repeated)) => true,
                _ => false
            });
    }
}
