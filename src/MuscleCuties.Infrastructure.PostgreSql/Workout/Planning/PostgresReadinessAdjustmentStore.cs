using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Stages optional-work changes and applies them only after a fresh server-side review.</summary>
public sealed class PostgresReadinessAdjustmentStore(PostgresDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReadinessAdjustmentProposalDto> ReviewAsync(Guid userId,
        ReviewReadinessAdjustmentRequest request, CancellationToken ct = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var tx = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, ct).ConfigureAwait(false);
            var state = await BuildStateAsync(userId, request, ct).ConfigureAwait(false);
            if (state.Error is not null)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ReadinessAdjustmentProposalDto(Guid.Empty, state.Band, state.Error, [], false);
            }

            if (state.Actions.Count == 0)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ReadinessAdjustmentProposalDto(Guid.Empty, state.Band, state.Summary, [], false);
            }

            var record = new WorkoutPlanProposalV02Record
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PlanId = state.PlanId,
                ExpectedRevisionId = state.ExpectedRevisionId,
                RulesetVersion = TodayCheckInScoreRule.RulesetVersion,
                TrainingPolicyVersion = WorkoutTrainingPolicyV020.Version,
                Status = "Proposed",
                InputSnapshotJson = JsonSerializer.Serialize(state.Snapshot, JsonOptions),
                ProposalJson = JsonSerializer.Serialize(state.Actions, JsonOptions),
                ReasonsJson = JsonSerializer.Serialize(new[] { state.Band }, JsonOptions),
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.WorkoutPlanProposalsV02.Add(record);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return ToDto(record, state.Actions, state.Band, state.Summary);
        }).ConfigureAwait(false);

    public async Task<ReadinessAdjustmentDecisionDto> DecideAsync(Guid userId,
        DecideReadinessAdjustmentRequest request, bool apply, CancellationToken ct = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var tx = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, ct).ConfigureAwait(false);
            var proposal = await dbContext.WorkoutPlanProposalsV02.SingleOrDefaultAsync(
                p => p.UserId == userId && p.Id == request.ProposalId, ct).ConfigureAwait(false)
                          ?? throw new KeyNotFoundException("Readiness proposal was not found.");
            if (proposal.Status != "Proposed")
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ReadinessAdjustmentDecisionDto(false, true, "This suggestion is no longer available. Review today’s readiness again.");
            }

            if (!apply)
            {
                proposal.Status = "Rejected";
                proposal.DecidedAtUtc = DateTime.UtcNow;
                proposal.ConcurrencyVersion++;
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ReadinessAdjustmentDecisionDto(false, false, "Your plan is unchanged.");
            }

            var state = await BuildStateAsync(userId,
                new ReviewReadinessAdjustmentRequest(request.LocalDate, request.TimeZoneId), ct).ConfigureAwait(false);
            var actionsJson = state.Error is null ? JsonSerializer.Serialize(state.Actions, JsonOptions) : string.Empty;
            var snapshotJson = state.Error is null ? JsonSerializer.Serialize(state.Snapshot, JsonOptions) : string.Empty;
            var latestRevisionId = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                .Where(r => r.UserId == userId && r.PlanId == proposal.PlanId)
                .OrderByDescending(r => r.RevisionNumber).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct)
                .ConfigureAwait(false) ?? Guid.Empty;
            if (state.Error is not null || state.Actions.Count == 0 || state.PlanId != proposal.PlanId ||
                proposal.ExpectedRevisionId != latestRevisionId || proposal.InputSnapshotJson != snapshotJson ||
                proposal.ProposalJson != actionsJson || proposal.RulesetVersion != TodayCheckInScoreRule.RulesetVersion ||
                proposal.TrainingPolicyVersion != WorkoutTrainingPolicyV020.Version ||
                proposal.ReasonsJson != JsonSerializer.Serialize(new[] { state.Band }, JsonOptions))
            {
                proposal.Status = "Expired";
                proposal.DecidedAtUtc = DateTime.UtcNow;
                proposal.ConcurrencyVersion++;
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ReadinessAdjustmentDecisionDto(false, true,
                    state.Error ?? "Today’s check-in or workout changed. Review a fresh suggestion before applying changes.");
            }

            var actions = JsonSerializer.Deserialize<List<ReadinessAction>>(proposal.ProposalJson, JsonOptions) ?? [];
            foreach (var action in actions)
            {
                switch (action.Kind)
                {
                    case "TrimOptionalSets":
                    {
                        var exercise = await dbContext.WorkoutDayExercises.SingleAsync(
                            e => e.UserId == userId && e.Id == action.ExerciseId, ct).ConfigureAwait(false);
                        exercise.Sets = action.AfterSets!.Value;
                        exercise.OptionalSets = 0;
                        break;
                    }
                    case "RemoveOptionalExercise":
                    {
                        var exercise = await dbContext.WorkoutDayExercises.SingleAsync(
                            e => e.UserId == userId && e.Id == action.ExerciseId, ct).ConfigureAwait(false);
                        exercise.IsRemovedByReadiness = true;
                        break;
                    }
                    case "RemoveOptionalSession":
                    {
                        var day = await dbContext.WorkoutDays.SingleAsync(
                            d => d.UserId == userId && d.Id == action.WorkoutDayId, ct).ConfigureAwait(false);
                        day.IsRemovedByReadiness = true;
                        break;
                    }
                    case "ChooseEasierAlternative":
                    {
                        var primary = await dbContext.WorkoutDays.SingleAsync(
                            d => d.UserId == userId && d.Id == action.WorkoutDayId, ct).ConfigureAwait(false);
                        var alternate = await dbContext.WorkoutDays.SingleAsync(
                            d => d.UserId == userId && d.Id == action.AlternateDayId, ct).ConfigureAwait(false);
                        primary.IsRemovedByReadiness = true;
                        if (alternate.Date == state.Snapshot.LocalDate ||
                            alternate.Date is null && alternate.DayOfWeek == (int)state.Snapshot.LocalDate.DayOfWeek)
                        {
                            alternate.IsOptional = false;
                            alternate.IsEasierAlternative = false;
                        }
                        else
                        {
                            var replacement = new WorkoutDayRecord
                            {
                                Id = Guid.CreateVersion7(),
                                UserId = userId,
                                WorkoutPlanId = alternate.WorkoutPlanId,
                                DayOfWeek = (int)state.Snapshot.LocalDate.DayOfWeek,
                                WorkoutType = alternate.WorkoutType,
                                Name = alternate.Name,
                                WeekIndex = alternate.WeekIndex,
                                Date = state.Snapshot.LocalDate,
                                IsOptional = false,
                                IsEasierAlternative = false
                            };
                            dbContext.WorkoutDays.Add(replacement);
                            var alternateExercises = await dbContext.WorkoutDayExercises.AsNoTracking()
                                .Where(e => e.UserId == userId && e.WorkoutDayId == alternate.Id && !e.IsRemovedByReadiness)
                                .ToListAsync(ct).ConfigureAwait(false);
                            dbContext.WorkoutDayExercises.AddRange(alternateExercises.Select(e => new WorkoutDayExerciseRecord
                            {
                                Id = Guid.CreateVersion7(),
                                UserId = userId,
                                WorkoutDayId = replacement.Id,
                                ExerciseId = e.ExerciseId,
                                Sets = e.Sets,
                                OptionalSets = 0,
                                Reps = e.Reps,
                                DurationSeconds = e.DurationSeconds,
                                IsOptional = false,
                                IsRemovedByReadiness = false
                            }));
                        }
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Readiness proposal contains an unsupported change.");
                }
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            var planDays = await dbContext.WorkoutDays.AsNoTracking()
                .Where(d => d.UserId == userId && d.WorkoutPlanId == proposal.PlanId)
                .OrderBy(d => d.Date).ThenBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false);
            var planDayIds = planDays.Select(d => d.Id).ToArray();
            var planExercises = await dbContext.WorkoutDayExercises.AsNoTracking()
                .Where(e => e.UserId == userId && planDayIds.Contains(e.WorkoutDayId))
                .OrderBy(e => e.Id).ToListAsync(ct).ConfigureAwait(false);
            var priorNumber = await dbContext.WorkoutPlanRevisionsV02
                .Where(r => r.UserId == userId && r.PlanId == proposal.PlanId)
                .Select(r => (long?)r.RevisionNumber).MaxAsync(ct).ConfigureAwait(false) ?? 0L;
            var revision = new WorkoutPlanRevisionV02Record
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PlanId = proposal.PlanId,
                RevisionNumber = checked(priorNumber + 1),
                SourceProposalId = proposal.Id,
                RulesetVersion = proposal.RulesetVersion,
                TrainingPolicyVersion = proposal.TrainingPolicyVersion,
                PlanJson = JsonSerializer.Serialize(new { Days = planDays, Exercises = planExercises }, JsonOptions),
                InputSnapshotJson = proposal.InputSnapshotJson,
                AcceptedAtUtc = DateTime.UtcNow
            };
            dbContext.WorkoutPlanRevisionsV02.Add(revision);
            proposal.Status = "Accepted";
            proposal.DecidedAtUtc = revision.AcceptedAtUtc;
            proposal.AcceptedRevisionId = revision.Id;
            proposal.ConcurrencyVersion++;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new ReadinessAdjustmentDecisionDto(true, false, "Suggested changes applied to today’s plan.");
        }).ConfigureAwait(false);

    private async Task<ReadinessState> BuildStateAsync(Guid userId,
        ReviewReadinessAdjustmentRequest request, CancellationToken ct)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return ReadinessState.Invalid("Check your device time zone and try again."); }
        var nowUtc = DateTimeOffset.UtcNow;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
        if (request.LocalDate != localToday)
            return ReadinessState.Invalid("Today changed. Refresh the page and review readiness again.");

        var checkIn = await dbContext.DailyReadinessLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.Date == localToday)
            .OrderByDescending(l => l.PlanningRevision).ThenByDescending(l => l.PlanningRecordedAtUtc)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var observation = checkIn is null ? null : new TodayCheckInObservation(
            checkIn.Date,
            checkIn.PlanningTimeZoneId ?? string.Empty,
            checkIn.PlanningRecordedAtUtc is { } recorded
                ? new DateTimeOffset(DateTime.SpecifyKind(recorded, DateTimeKind.Utc))
                : DateTimeOffset.MinValue,
            checkIn.PlanningRevision,
            checkIn.PlanningSleepQuality,
            checkIn.Energy,
            checkIn.PlanningSoreness,
            checkIn.PlanningStress);
        var score = TodayCheckInScoreRule.Evaluate(observation, nowUtc, request.TimeZoneId);
        var localDayStart = DateTime.SpecifyKind(localToday.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localDayEnd = localDayStart.AddDays(1);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDayStart, zone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localDayEnd, zone);
        var activeSession = await dbContext.WorkoutSessionLogs.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && !s.IsDeleted && s.CompletedAt == null, ct).ConfigureAwait(false);
        if (activeSession)
            return ReadinessState.Invalid("Finish your current session before reviewing readiness changes.");
        var sessionActivities = await dbContext.WorkoutSessionLogs.AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted && s.CompletedAt != null &&
                        s.StartedAt >= dayStartUtc && s.StartedAt < dayEndUtc)
            .OrderBy(s => s.StartedAt)
            .Select(s => new CompletedActivitySnapshot(s.Id, s.WorkoutName, s.StartedAt,
                s.CompletedAt, s.TotalDurationSeconds, s.TotalVolumeKg, s.RpeRating))
            .ToListAsync(ct).ConfigureAwait(false);
        var hasSymptoms = await dbContext.SymptomLogs.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Date == localToday, ct).ConfigureAwait(false);
        var hasRestrictions = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .AnyAsync(i => i.UserId == userId && i.Status != "Cleared", ct).ConfigureAwait(false);
        var decision = TodayPlanningDecisionRule.Evaluate(new TodayPlanningContext(
            hasSymptoms, false, hasRestrictions, score));
        if (decision.Status == TodayPlanningStatus.RestrictionBlocked)
            return ReadinessState.Invalid("Your current restrictions take priority. No readiness changes are suggested.");
        if (decision.Status == TodayPlanningStatus.SymptomReviewRequired)
            return ReadinessState.Invalid("Your reported symptoms take priority over this score. No readiness changes are suggested.");
        if (score.Status != CheckInScoreStatus.Available || score.Band is null)
            return ReadinessState.Invalid("Complete today’s check-in to see suggestions based on how you feel.");
        if (score.Band == CheckInScoreBand.FeelingReady)
            return ReadinessState.Empty("FeelingReady", "Your check-in suggests keeping your planned session. You don’t need to add more.");

        var plan = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (plan is null)
            return ReadinessState.Empty(BandName(score.Band.Value), "There’s no active session to adjust today.");

        var expectedRevisionId = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(r => r.UserId == userId && r.PlanId == plan.Id)
            .OrderByDescending(r => r.RevisionNumber).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? Guid.Empty;
        var dayOfWeek = (int)localToday.DayOfWeek;
        var days = await dbContext.WorkoutDays.AsNoTracking()
            .Where(d => d.UserId == userId && d.WorkoutPlanId == plan.Id && !d.IsRemovedByReadiness &&
                        (d.Date == localToday || d.Date == null && d.DayOfWeek == dayOfWeek))
            .OrderBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false);
        var easierAlternatives = score.Band == CheckInScoreBand.ConsiderEasierDay
            ? await dbContext.WorkoutDays.AsNoTracking()
                .Where(d => d.UserId == userId && d.WorkoutPlanId == plan.Id && !d.IsRemovedByReadiness &&
                            d.IsOptional && d.IsEasierAlternative && d.WorkoutType != nameof(WorkoutType.Rest))
                .OrderBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false)
            : [];
        var allRelevantDays = days.Concat(easierAlternatives).DistinctBy(d => d.Id).ToArray();
        var dayIds = allRelevantDays.Select(d => d.Id).ToArray();
        var dayExercises = dayIds.Length == 0 ? [] : await dbContext.WorkoutDayExercises.AsNoTracking()
            .Where(e => e.UserId == userId && dayIds.Contains(e.WorkoutDayId) && !e.IsRemovedByReadiness)
            .OrderBy(e => e.Id).ToListAsync(ct).ConfigureAwait(false);
        var workoutLogs = dayIds.Length == 0 ? [] : await dbContext.WorkoutLogs.AsNoTracking()
            .Where(l => l.UserId == userId && dayIds.Contains(l.WorkoutDayId) && l.Date == localToday)
            .ToListAsync(ct).ConfigureAwait(false);
        var loggedDayIds = workoutLogs.Select(l => l.WorkoutDayId).ToHashSet();
        var completedDayIds = workoutLogs.Where(l => l.CompletionPercent >= 100).Select(l => l.WorkoutDayId).ToHashSet();
        var todayDayIds = days.Select(d => d.Id).ToHashSet();
        var logIds = workoutLogs.Select(l => l.Id).ToArray();
        var loggedExerciseIds = logIds.Length == 0 ? new HashSet<Guid>() : (await dbContext.WorkoutExerciseLogs.AsNoTracking()
            .Where(l => l.UserId == userId && logIds.Contains(l.WorkoutLogId))
            .Select(l => l.WorkoutDayExerciseId).Distinct().ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
        var exerciseIds = dayExercises.Select(e => e.ExerciseId).Distinct().ToArray();
        var exerciseNames = exerciseIds.Length == 0 ? new Dictionary<int, string>() : await dbContext.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct).ConfigureAwait(false);
        var currentRevisionId = expectedRevisionId;
        var snapshot = new ReadinessInputSnapshot(localToday, request.TimeZoneId, checkIn!.PlanningRevision,
            checkIn.PlanningRecordedAtUtc, checkIn.PlanningSleepQuality, checkIn.Energy,
            checkIn.PlanningSoreness, checkIn.PlanningStress, score.RawScore, score.Band!.Value,
            plan.Id, currentRevisionId,
            allRelevantDays.Select(d => new SessionInputSnapshot(d.Id, d.Date, d.WorkoutType, d.Name,
                d.IsOptional, d.IsEasierAlternative,
                loggedDayIds.Contains(d.Id), dayExercises.Where(e => e.WorkoutDayId == d.Id)
                    .Select(e => new ExerciseInputSnapshot(e.Id, e.ExerciseId, e.Sets, e.OptionalSets, e.IsOptional,
                        loggedExerciseIds.Contains(e.Id))).ToArray())).ToArray(), sessionActivities);

        var candidateExercises = dayExercises.Where(e => todayDayIds.Contains(e.WorkoutDayId) &&
            !(score.Band == CheckInScoreBand.ConsiderEasierDay &&
              easierAlternatives.Any(d => d.Id == e.WorkoutDayId)));
        var actions = candidateExercises.Where(e => !loggedExerciseIds.Contains(e.Id) &&
                !completedDayIds.Contains(e.WorkoutDayId) && e.OptionalSets > 0)
            .Select(e => new ReadinessAction("TrimOptionalSets", e.WorkoutDayId, e.Id, null,
                e.Sets, e.Sets - e.OptionalSets,
                $"Reduce {e.OptionalSets} optional set(s) from {exerciseNames.GetValueOrDefault(e.ExerciseId, "an exercise")}."))
            .ToList();
        if (actions.Count == 0)
        {
            actions = candidateExercises.Where(e => e.IsOptional && !loggedExerciseIds.Contains(e.Id) &&
                                               !completedDayIds.Contains(e.WorkoutDayId))
                .Select(e => new ReadinessAction("RemoveOptionalExercise", e.WorkoutDayId, e.Id, null,
                    null, null, $"Remove {exerciseNames.GetValueOrDefault(e.ExerciseId, "an exercise")} you marked optional.")).ToList();
        }
        if (actions.Count == 0)
        {
            actions = days.Where(d => d.IsOptional && !loggedDayIds.Contains(d.Id) && !completedDayIds.Contains(d.Id) &&
                                      (score.Band != CheckInScoreBand.ConsiderEasierDay || !d.IsEasierAlternative))
                .Select(d => new ReadinessAction("RemoveOptionalSession", d.Id, null, null,
                    null, null, $"Remove {d.Name}, a session you marked optional.")).ToList();
        }
        if (actions.Count == 0 && score.Band == CheckInScoreBand.ConsiderEasierDay)
        {
            var primaryDays = days.Where(d => !d.IsOptional &&
                !string.Equals(d.WorkoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase) &&
                !loggedDayIds.Contains(d.Id) && !completedDayIds.Contains(d.Id)).ToArray();
            var alternate = easierAlternatives.FirstOrDefault(d => !loggedDayIds.Contains(d.Id));
            if (primaryDays.Length > 0 && alternate is not null)
                actions.AddRange(primaryDays.Select(d => new ReadinessAction("ChooseEasierAlternative", d.Id,
                    null, alternate.Id, null, null, $"Replace {d.Name} with {alternate.Name}, your marked easier alternative.")));
        }

        if (actions.Count == 0)
        {
            var summary = score.Band == CheckInScoreBand.ConsiderEasierDay
                ? "No optional work or eligible easier alternative is available to change today’s plan."
                : "No optional work is marked for today, so there are no suggested changes.";
            return ReadinessState.Empty(BandName(score.Band.Value), summary);
        }
        var finalSummary = score.Band == CheckInScoreBand.ConsiderEasierDay
            ? "Review these changes as an easier-day option."
            : "Review these changes to lighten today’s plan.";
        return new ReadinessState(BandName(score.Band.Value), finalSummary, actions, snapshot,
            plan.Id, currentRevisionId, null);
    }

    private static ReadinessAdjustmentProposalDto ToDto(WorkoutPlanProposalV02Record record,
        IReadOnlyList<ReadinessAction> actions, string band, string summary) => new(record.Id, band, summary,
        actions.Select(a => new ReadinessAdjustmentChange(a.Kind, a.Description, a.BeforeSets, a.AfterSets)).ToArray(), true);

    private static string BandName(CheckInScoreBand band) => band switch
    {
        CheckInScoreBand.FeelingReady => "FeelingReady",
        CheckInScoreBand.ConsiderLighterSession => "ConsiderLighterSession",
        _ => "ConsiderEasierDay"
    };

    private sealed record ReadinessAction(string Kind, Guid WorkoutDayId, Guid? ExerciseId, Guid? AlternateDayId,
        int? BeforeSets, int? AfterSets, string Description);
    private sealed record ExerciseInputSnapshot(Guid Id, int ExerciseId, int Sets, int OptionalSets,
        bool IsOptional, bool IsLogged);
    private sealed record SessionInputSnapshot(Guid Id, DateOnly? Date, string WorkoutType, string Name,
        bool IsOptional, bool IsEasierAlternative, bool IsLogged,
        IReadOnlyList<ExerciseInputSnapshot> Exercises);
    private sealed record CompletedActivitySnapshot(Guid Id, string Name, DateTime StartedAtUtc,
        DateTime? CompletedAtUtc, int DurationSeconds, decimal VolumeKg, int? RpeRating);
    private sealed record ReadinessInputSnapshot(DateOnly LocalDate, string TimeZoneId, long CheckInRevision,
        DateTime? CheckInRecordedAtUtc, int? SleepQuality, int? Energy, int? Soreness, int? Stress,
        decimal? RawScore, CheckInScoreBand Band, Guid PlanId, Guid ExpectedRevisionId,
        IReadOnlyList<SessionInputSnapshot> Sessions,
        IReadOnlyList<CompletedActivitySnapshot> CompletedActivities);
    private sealed record ReadinessState(string Band, string Summary, List<ReadinessAction> Actions,
        ReadinessInputSnapshot Snapshot, Guid PlanId, Guid ExpectedRevisionId, string? Error)
    {
        public static ReadinessState Invalid(string message) => new("Unavailable", message, [], new ReadinessInputSnapshot(
            default, string.Empty, 0, null, null, null, null, null, null, CheckInScoreBand.ConsiderEasierDay,
            Guid.Empty, Guid.Empty, [], []), Guid.Empty, Guid.Empty, message);
        public static ReadinessState Empty(string band, string message) => new(band, message, [], new ReadinessInputSnapshot(
            default, string.Empty, 0, null, null, null, null, null, null, CheckInScoreBand.ConsiderEasierDay,
            Guid.Empty, Guid.Empty, [], []), Guid.Empty, Guid.Empty, null);
    }
}
