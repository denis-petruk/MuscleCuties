using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// Server-side reconciliation engine for workout plan automatic updates:
/// - Cleared injury 7-calendar-day optional window and restoration.
/// - Experience promotion deferred during active preview lock, applied at the next scheduled workout boundary.
/// - Safety answer transitions restoring suppressed days for applied preferences.
/// </summary>
public sealed class PostgresWorkoutPlanReconciler(PostgresDbContext context) : IWorkoutPlanReconciler
{
    private const string ClearedStatus = "Cleared";
    private const int CompletedWorkoutPercent = 100;
    private const long NextRevisionIncrement = 1;

    public async Task ReconcileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return;

        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(context, userId, cancellationToken).ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(context, userId, cancellationToken).ConfigureAwait(false);

            var state = await context.WorkoutAccountPlanningStates.SingleOrDefaultAsync(
                row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
            if (state?.TimeZoneId is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(state.TimeZoneId);
            }
            catch (Exception)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var now = new DateTimeOffset(nowUtc, TimeSpan.Zero);
            var today = WorkoutAccountCalendarRule.LocalDate(now, zone);

            var activePlan = await context.WorkoutPlans
                .Where(plan => plan.UserId == userId && plan.IsActive)
                .OrderByDescending(plan => plan.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            // 1. Cleared injury exercise and day restoration
            var clearedInjuries = await context.WorkoutInjuryLogs.AsNoTracking()
                .Where(injury => injury.UserId == userId && injury.Status == ClearedStatus && injury.ClearedDate != null)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (clearedInjuries.Count > 0 && activePlan is not null)
            {
                foreach (var injury in clearedInjuries)
                {
                    if (injury.ClearedDate is not { } clearanceDate)
                        continue;

                    // Unsuppress any upcoming days that were suppressed by this cleared injury
                    var suppressedDays = await context.WorkoutDays
                        .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlan.Id &&
                                      day.SafetyInjuryId == injury.Id && day.IsRemovedBySafety &&
                                      day.Date >= today)
                        .ToListAsync(cancellationToken).ConfigureAwait(false);

                    foreach (var day in suppressedDays)
                    {
                        day.IsRemovedBySafety = false;
                        day.SafetyInjuryId = null;
                    }

                    // Enforce the 7-calendar-day optional exercise window for restored exercises
                    var requiredFromDate = WorkoutInjuryClearanceRule.RequiredFromLocalDate(clearanceDate);
                    if (requiredFromDate is { } requiredFrom)
                    {
                        var affectedDayIds = await context.WorkoutDays.AsNoTracking()
                            .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlan.Id &&
                                          day.Date >= today && day.Date >= clearanceDate)
                            .Select(day => day.Id)
                            .ToListAsync(cancellationToken).ConfigureAwait(false);

                        if (affectedDayIds.Count > 0)
                        {
                            var affectedExercises = await context.WorkoutDayExercises
                                .Where(exercise => exercise.UserId == userId &&
                                                   affectedDayIds.Contains(exercise.WorkoutDayId))
                                .ToListAsync(cancellationToken).ConfigureAwait(false);

                            var daysById = await context.WorkoutDays.AsNoTracking()
                                .Where(day => affectedDayIds.Contains(day.Id))
                                .ToDictionaryAsync(day => day.Id, day => day.Date, cancellationToken).ConfigureAwait(false);

                            foreach (var exercise in affectedExercises)
                            {
                                if (daysById.TryGetValue(exercise.WorkoutDayId, out var dayDate) && dayDate is { } date)
                                {
                                    var shouldBeOptional = date < requiredFrom;
                                    if (exercise.IsOptional != shouldBeOptional)
                                    {
                                        exercise.IsOptional = shouldBeOptional;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 2. Safety answer restoration (when an answer makes an activity safe again)
            if (state.SafetyRegenerationPending)
            {
                var isPreviewOpen = await WorkoutSessionAccountLock.HasActivePreviewAsync(
                    context, userId, nowUtc, cancellationToken).ConfigureAwait(false);

                if (!isPreviewOpen)
                {
                    if (activePlan is not null)
                    {
                        // Restore days suppressed by safety where the activity is safe
                        var suppressedDays = await context.WorkoutDays
                            .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlan.Id &&
                                          day.IsRemovedBySafety && day.Date >= today)
                            .ToListAsync(cancellationToken).ConfigureAwait(false);

                        foreach (var day in suppressedDays)
                        {
                            day.IsRemovedBySafety = false;
                            day.SafetyActivityTypeId = null;
                        }
                    }

                    state.SafetyRegenerationPending = false;
                }
            }

            // 3. Promotion-driven plan updates (deferred if preview is open)
            if (state.PromotionPending)
            {
                var isPreviewOpen = await WorkoutSessionAccountLock.HasActivePreviewAsync(
                    context, userId, nowUtc, cancellationToken).ConfigureAwait(false);

                if (!isPreviewOpen)
                {
                    if (activePlan is not null)
                    {
                        // Find the next scheduled workout day as the revision boundary
                        var nextScheduledDay = await context.WorkoutDays.AsNoTracking()
                            .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlan.Id &&
                                          day.Date >= today && !day.IsRemovedBySafety && !day.IsRemovedByReadiness &&
                                          !context.WorkoutLogs.Any(log => log.UserId == userId &&
                                              log.WorkoutDayId == day.Id && log.CompletionPercent >= CompletedWorkoutPercent))
                            .OrderBy(day => day.Date)
                            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                        if (nextScheduledDay is not null)
                        {
                            var latestRevision = await context.WorkoutPlanRevisionsV02.AsNoTracking()
                                .Where(rev => rev.UserId == userId && rev.PlanId == activePlan.Id)
                                .OrderByDescending(rev => rev.RevisionNumber)
                                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                            var newRevisionNumber = (latestRevision?.RevisionNumber ?? 0) + NextRevisionIncrement;
                            var revisionRecord = new WorkoutPlanRevisionV02Record
                            {
                                Id = Guid.CreateVersion7(),
                                UserId = userId,
                                PlanId = activePlan.Id,
                                RevisionNumber = newRevisionNumber,
                                SourceProposalId = latestRevision?.SourceProposalId ?? Guid.Empty,
                                RulesetVersion = latestRevision?.RulesetVersion ?? string.Empty,
                                TrainingPolicyVersion = latestRevision?.TrainingPolicyVersion ?? string.Empty,
                                PlanJson = latestRevision?.PlanJson ?? "{}",
                                InputSnapshotJson = latestRevision?.InputSnapshotJson ?? "{}",
                                AcceptedAtUtc = nowUtc
                            };
                            context.WorkoutPlanRevisionsV02.Add(revisionRecord);
                        }
                    }

                    state.PromotionPending = false;
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
