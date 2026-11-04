using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Adjusts only newly materialized occurrences under the caller's account transaction.</summary>
internal static class PostgresCanonicalOccurrenceAdjustment
{
    internal static async Task ApplyAsync(PostgresDbContext context, Guid userId, Guid? previousPlanId,
        DateOnly today, DateOnly firstDate, TimeZoneInfo zone,
        IReadOnlyList<StrengthPlanPreviewSessionV020> previewSessions,
        List<WorkoutDayRecord> days, IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        CancellationToken cancellationToken)
    {
        // A safe non-strength remainder must not consume a deload window.
        if (exercises.Count == 0) return;
        var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(s => s.UserId == userId,
            cancellationToken).ConfigureAwait(false);
        if (state is null || state.TimeZoneId != zone.Id || previousPlanId is null) return;
        var dates = days.Where(d => d.Date.HasValue).ToDictionary(d => d.Id, d => d.Date!.Value);
        // An unknown effort target cannot be silently replaced with an invented one.
        if (exercises.Any(e => !dates.ContainsKey(e.WorkoutDayId) || e.TargetRepetitionsInReserve is null ||
            e.TargetRepetitionsInReserve < 0 || e.Sets <= 0)) return;
        var offset = ((int)today.DayOfWeek + CanonicalRecoveryEvidenceRule.DaysPerWeek - (int)DayOfWeek.Monday)
            % CanonicalRecoveryEvidenceRule.DaysPerWeek;
        if (today.DayNumber < offset || firstDate.DayNumber > DateOnly.MaxValue.DayNumber - CanonicalOccurrenceAdjustmentRule.DeloadDurationDays) return;
        var evidenceEnd = today.AddDays(-offset);
        DateOnly start, end;
        if (state.DeloadStartDate is { } priorStart && state.DeloadEndExclusive is { } priorEnd &&
            state.DeloadEvidenceThroughExclusive is not null && priorEnd > today && priorEnd > priorStart)
        {
            start = priorStart;
            end = priorEnd;
        }
        else
        {
            if (state.DeloadEvidenceThroughExclusive >= evidenceEnd) return;
            var evidence = await PostgresCanonicalRecoveryEvidence.EvaluateAsync(context, userId, previousPlanId.Value,
                today, zone, previewSessions, cancellationToken).ConfigureAwait(false);
            if (!evidence.IsDeloadTriggered) return;
            start = firstDate;
            end = start.AddDays(CanonicalOccurrenceAdjustmentRule.DeloadDurationDays);
            state.DeloadEvidenceThroughExclusive = evidenceEnd;
            state.DeloadStartDate = start;
            state.DeloadEndExclusive = end;
        }
        foreach (var exercise in exercises.Where(e => CanonicalOccurrenceAdjustmentRule.IsInDeloadWindow(
            dates[e.WorkoutDayId], start, end)))
        {
            var adjusted = CanonicalOccurrenceAdjustmentRule.Deload(exercise.Sets, exercise.TargetRepetitionsInReserve!.Value);
            exercise.Sets = adjusted.Sets;
            exercise.TargetRepetitionsInReserve = adjusted.TargetRir;
            exercise.OptionalSets = Math.Min(exercise.OptionalSets, exercise.Sets - 1);
        }
        var strengthDates = exercises.Select(exercise => dates[exercise.WorkoutDayId]).ToHashSet();
        days.RemoveAll(day => day.Date is { } date && day.ActivityTypeId is { } activity &&
            CanonicalOccurrenceAdjustmentRule.RemoveOptionalHardCompanion(day.IsOptional,
                (WorkoutActivityType)activity, strengthDates.Contains(date), date, start, end) &&
            !exercises.Any(exercise => exercise.WorkoutDayId == day.Id));
    }
}
