using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Accepts an immutable feasible proposal and publishes the selected weekly split.</summary>
public sealed class PostgresAcceptStrengthPlanProposalStoreV020(PostgresDbContext dbContext)
    : IAcceptStrengthPlanProposalStoreV020
{
    private const int OverviewScheduleWeeks = 4;
    private const int DaysPerWeek = 7;
    private const long InitialRevisionNumber = 1;
    private const int MinimumSessionIndex = 0;
    private const int MinimumExerciseSetCount = 1;
    private const int MinimumRepetitionCount = 1;
    private const int MondayWeekOrderOffset = 1;
    private const decimal MinimumPlannedActivityDurationMinutes = 0m;
    private const string PlannedDurationMinuteFormat = "0.#";
    private const string MixedActivityPlanName = "Mixed activity plan";
    private const string RestDayName = "Rest day";
    private const string MixedScheduleInvalidCode = "ProposalMixedScheduleInvalid";
    private const string UnsupportedOtherActivityCode = "WorkoutTypeCannotRepresentOtherActivity";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AcceptStrengthPlanProposalResultV020> AcceptAsync(Guid userId,
        AcceptStrengthPlanProposalRequestV020 request, CancellationToken cancellationToken) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var proposal = await dbContext.WorkoutPlanProposalsV02.SingleOrDefaultAsync(
                    item => item.UserId == userId && item.Id == request.ProposalId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (proposal is null)
                return await FinishAsync(transaction, new("NotFound", null, null, "ProposalNotFound"),
                    cancellationToken).ConfigureAwait(false);
            if (proposal.Status != "Proposed")
                return await FinishAsync(transaction, new("Conflict", proposal.PlanId, null,
                    "ProposalAlreadyDecided"), cancellationToken).ConfigureAwait(false);

            var proposalSnapshot = Deserialize<ProposalSnapshot>(proposal.ProposalJson);
            var inputSnapshot = Deserialize<ProposalInputSnapshot>(proposal.InputSnapshotJson);
            if (proposalSnapshot is null || inputSnapshot is null ||
                proposalSnapshot.Status != "Feasible" || proposalSnapshot.Sessions is not { Count: > 0 } ||
                proposalSnapshot.PolicyId != StrengthPlanPreviewRule.PolicyId ||
                proposalSnapshot.PolicyVersion != StrengthPlanPreviewRule.Version ||
                proposalSnapshot.RulesetVersion != StrengthPlanPreviewRule.RulesetVersion)
                return await FinishAsync(transaction, new("InvalidProposal", null, null,
                    "ProposalSnapshotInvalid"), cancellationToken).ConfigureAwait(false);

            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(plan => plan.UserId == userId && plan.IsActive)
                .OrderByDescending(plan => plan.CreatedAt)
                .Select(plan => (Guid?)plan.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var activeRevisionId = activePlanId is { } existingPlanId
                ? await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                    .Where(revision => revision.UserId == userId && revision.PlanId == existingPlanId)
                    .OrderByDescending(revision => revision.RevisionNumber)
                    .Select(revision => (Guid?)revision.Id)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                : null;
            if (activePlanId != inputSnapshot.BaselineActivePlanId ||
                activeRevisionId != inputSnapshot.BaselineRevisionId)
            {
                proposal.Status = "Expired";
                proposal.DecidedAtUtc = DateTime.UtcNow;
                proposal.ConcurrencyVersion++;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await FinishAsync(transaction, new("Stale", proposal.PlanId, null,
                    "ActivePlanChangedSinceProposal"), cancellationToken).ConfigureAwait(false);
            }

            var currentProfile = await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => new { profile.ConcurrencyVersion, profile.AvailableWorkoutWeekdaysJson })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (currentProfile is null || currentProfile.ConcurrencyVersion != inputSnapshot.ProfileVersion)
            {
                proposal.Status = "Expired";
                proposal.DecidedAtUtc = DateTime.UtcNow;
                proposal.ConcurrencyVersion++;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await FinishAsync(transaction, new("Stale", proposal.PlanId, null,
                    "WorkoutPreferencesChangedSinceProposal"), cancellationToken).ConfigureAwait(false);
            }
            var availableWeekdays = ParseAvailableWeekdays(currentProfile.AvailableWorkoutWeekdaysJson);

            if (!TryGetMixedSchedule(proposalSnapshot, availableWeekdays, out var scheduleSessions) ||
                !TryGetStrengthSessions(proposalSnapshot.Sessions, scheduleSessions, availableWeekdays,
                    out var strengthSessions, out var assignments))
            {
                var reasonCode = ContainsUnrepresentableOtherActivity(proposalSnapshot)
                    ? UnsupportedOtherActivityCode
                    : MixedScheduleInvalidCode;
                return await FinishAsync(transaction, new("Limited", null, null,
                    reasonCode), cancellationToken).ConfigureAwait(false);
            }

            var nowUtc = DateTime.UtcNow;
            var revisionId = Guid.CreateVersion7();
            var plan = new WorkoutPlanRecord
            {
                Id = proposal.PlanId,
                UserId = userId,
                Name = MixedActivityPlanName,
                IsActive = true,
                CreatedAt = nowUtc
            };
            if (activePlanId is { } priorPlanId)
            {
                var previous = await dbContext.WorkoutPlans.SingleAsync(
                    item => item.UserId == userId && item.Id == priorPlanId, cancellationToken)
                    .ConfigureAwait(false);
                previous.IsActive = false;
            }
            dbContext.WorkoutPlans.Add(plan);

            var sessionByWeekday = scheduleSessions.ToDictionary(session => session.Weekday);
            var weekStart = MondayOf(inputSnapshot.Request.LocalDate);
            var planDays = new List<WorkoutDayRecord>(OverviewScheduleWeeks * DaysPerWeek);
            var exercises = new List<WorkoutDayExerciseRecord>();
            for (var weekIndex = 0; weekIndex < OverviewScheduleWeeks; weekIndex++)
            for (var dayOffset = 0; dayOffset < DaysPerWeek; dayOffset++)
            {
                var date = weekStart.AddDays(weekIndex * DaysPerWeek + dayOffset);
                var weekday = date.DayOfWeek;
                var dayId = Guid.CreateVersion7();
                sessionByWeekday.TryGetValue(weekday, out var session);
                var activity = session is { } scheduledSession
                    ? (WorkoutActivityType)scheduledSession.ActivityTypeId
                    : WorkoutActivityType.RestDay;
                var workoutDay = new WorkoutDayRecord
                {
                    Id = dayId,
                    UserId = userId,
                    WorkoutPlanId = plan.Id,
                    DayOfWeek = (int)weekday,
                    WorkoutType = session is not null ? ToWorkoutType(activity) : nameof(WorkoutType.Rest),
                    Name = session is not null ? ActivityName(activity) : RestDayName,
                    WeekIndex = weekIndex,
                    Date = date,
                    PlannedMainDurationMinutes = session?.DurationMinutes
                };
                planDays.Add(workoutDay);
                if (session is null || !IsStrengthActivity(activity) ||
                    !strengthSessions.TryGetValue(weekday, out var strengthSession))
                    continue;
                foreach (var slot in strengthSession.Slots.Where(slot => slot.ExerciseId.HasValue &&
                                                                  slot.Sets is >= MinimumExerciseSetCount &&
                                                                  slot.MinimumRepetitions is >= MinimumRepetitionCount))
                    exercises.Add(new WorkoutDayExerciseRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        WorkoutDayId = dayId,
                        ExerciseId = slot.ExerciseId!.Value,
                        Sets = slot.Sets!.Value,
                        Reps = slot.MinimumRepetitions!.Value,
                        IsOptional = !slot.IsRequired
                    });
            }

            dbContext.WorkoutDays.AddRange(planDays);
            dbContext.WorkoutDayExercises.AddRange(exercises);
            dbContext.WorkoutPlanRevisionsV02.Add(new WorkoutPlanRevisionV02Record
            {
                Id = revisionId,
                UserId = userId,
                PlanId = plan.Id,
                RevisionNumber = InitialRevisionNumber,
                SourceProposalId = proposal.Id,
                RulesetVersion = proposal.RulesetVersion,
                TrainingPolicyVersion = proposal.TrainingPolicyVersion,
                PlanJson = JsonSerializer.Serialize(new AcceptedPlanSnapshot(
                    proposalSnapshot, assignments, scheduleSessions, OverviewScheduleWeeks), JsonOptions),
                InputSnapshotJson = proposal.InputSnapshotJson,
                AcceptedAtUtc = nowUtc
            });
            proposal.Status = "Accepted";
            proposal.DecidedAtUtc = nowUtc;
            proposal.AcceptedRevisionId = revisionId;
            proposal.ConcurrencyVersion++;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AcceptStrengthPlanProposalResultV020("Accepted", plan.Id, revisionId, null);
        }).ConfigureAwait(false);

    private static async Task<AcceptStrengthPlanProposalResultV020> FinishAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        AcceptStrengthPlanProposalResultV020 result, CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - MondayWeekOrderOffset + DaysPerWeek) % DaysPerWeek;
        return date.AddDays(-offset);
    }

    private static bool TryGetMixedSchedule(ProposalSnapshot proposal,
        IReadOnlySet<DayOfWeek> availableWeekdays,
        out MixedActivityScheduleSessionV020[] scheduleSessions)
    {
        scheduleSessions = [];
        var schedule = proposal.MixedActivitySchedule;
        if (schedule is null || schedule.Sessions is not { Count: > 0 } || schedule.Activities is null ||
            schedule.Sessions.Count > availableWeekdays.Count)
            return false;

        scheduleSessions = schedule.Sessions.ToArray();
        var decisions = schedule.Activities;
        if (decisions.Any(decision => decision is null ||
                                      !Enum.IsDefined((WorkoutActivityType)decision.ActivityTypeId) ||
                                      decision.ScheduledSessionsPerWeek < 0 ||
                                      !IsValidDisposition(decision.Disposition)) ||
            decisions.Select(decision => decision.ActivityTypeId).Distinct().Count() != decisions.Count)
            return false;

        var decisionByActivity = decisions.ToDictionary(decision => decision.ActivityTypeId);
        if (scheduleSessions.Any(session => session is null ||
                !IsPersistableActivity((WorkoutActivityType)session.ActivityTypeId) ||
                !Enum.IsDefined(session.Weekday) || !availableWeekdays.Contains(session.Weekday) ||
                session.DurationMinutes <= MinimumPlannedActivityDurationMinutes ||
                !decisionByActivity.TryGetValue(session.ActivityTypeId, out var decision) ||
                !IsScheduledDisposition(decision.Disposition)))
            return false;

        if (scheduleSessions.Select(session => session.Weekday).Distinct().Count() != scheduleSessions.Length)
            return false;

        var scheduledCountByActivity = scheduleSessions
            .GroupBy(session => session.ActivityTypeId)
            .ToDictionary(group => group.Key, group => group.Count());
        return decisions.All(decision =>
            decision.ScheduledSessionsPerWeek ==
            (scheduledCountByActivity.TryGetValue(decision.ActivityTypeId, out var count) ? count : 0) &&
            ((decision.ScheduledSessionsPerWeek > 0) == IsScheduledDisposition(decision.Disposition)));
    }

    private static bool ContainsUnrepresentableOtherActivity(ProposalSnapshot proposal) =>
        proposal.MixedActivitySchedule?.Sessions?.Any(session => session is not null &&
            session.ActivityTypeId == (int)WorkoutActivityType.Other) == true;

    private static bool TryGetStrengthSessions(IReadOnlyList<StrengthPlanPreviewSessionV020> proposalSessions,
        IReadOnlyList<MixedActivityScheduleSessionV020> scheduleSessions,
        IReadOnlySet<DayOfWeek> availableWeekdays,
        out IReadOnlyDictionary<DayOfWeek, StrengthPlanPreviewSessionV020> strengthSessions,
        out IReadOnlyList<StrengthWeekdayAssignment> assignments)
    {
        strengthSessions = new Dictionary<DayOfWeek, StrengthPlanPreviewSessionV020>();
        assignments = [];
        if (proposalSessions.Any(session => session is null))
            return false;

        var orderedSessions = proposalSessions.OrderBy(session => session.SessionIndex).ToArray();
        var plannedWeekdays = orderedSessions.Select(session => session.PlannedWeekday).ToArray();
        if (orderedSessions.Any(session => session.SessionIndex < MinimumSessionIndex || session.Slots is null) ||
            orderedSessions.Select(session => session.SessionIndex).Distinct().Count() != orderedSessions.Length ||
            plannedWeekdays.Any(weekday => weekday is null || !Enum.IsDefined(weekday.Value) ||
                                           !availableWeekdays.Contains(weekday.Value)) ||
            plannedWeekdays.Distinct().Count() != orderedSessions.Length)
            return false;

        var strengthSchedule = scheduleSessions
            .Where(session => IsStrengthActivity((WorkoutActivityType)session.ActivityTypeId))
            .ToArray();
        if (strengthSchedule.Length != orderedSessions.Length ||
            strengthSchedule.Any(session => !plannedWeekdays.Contains(session.Weekday)))
            return false;

        strengthSessions = orderedSessions.ToDictionary(session => session.PlannedWeekday!.Value);
        assignments = orderedSessions.Select(session =>
            new StrengthWeekdayAssignment(session.SessionIndex, session.PlannedWeekday!.Value)).ToArray();
        return true;
    }

    private static bool IsValidDisposition(string disposition) =>
        Enum.TryParse<MixedActivityScheduleDisposition>(disposition, true, out var parsed) &&
        Enum.IsDefined(parsed);

    private static bool IsScheduledDisposition(string disposition) =>
        Enum.TryParse<MixedActivityScheduleDisposition>(disposition, true, out var parsed) &&
        parsed is MixedActivityScheduleDisposition.Scheduled or MixedActivityScheduleDisposition.PartiallyScheduled;

    private static bool IsStrengthActivity(WorkoutActivityType activity) => activity is
        WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.GeneralStrength;

    private static bool IsPersistableActivity(WorkoutActivityType activity) =>
        Enum.IsDefined(activity) && activity is not WorkoutActivityType.RestDay and not WorkoutActivityType.Other;

    private static string ToWorkoutType(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.GeneralStrength or WorkoutActivityType.RockClimbing => nameof(WorkoutType.Strength),
        WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery =>
            nameof(WorkoutType.Recovery),
        WorkoutActivityType.Hiit or WorkoutActivityType.Cycling or WorkoutActivityType.Running or
            WorkoutActivityType.Swimming or WorkoutActivityType.Walking or WorkoutActivityType.Rowing or
            WorkoutActivityType.Elliptical => nameof(WorkoutType.Cardio),
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unsupported scheduled activity.")
    };

    private static string ActivityName(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength => "High volume strength",
        WorkoutActivityType.StrengthHighIntensity => "Strength high intensity",
        WorkoutActivityType.GeneralStrength => "General strength training",
        WorkoutActivityType.RockClimbing => "Rock climbing",
        WorkoutActivityType.Yoga => "Yoga",
        WorkoutActivityType.Hiit => "HIIT",
        WorkoutActivityType.Cycling => "Cycling",
        WorkoutActivityType.Running => "Running",
        WorkoutActivityType.Swimming => "Swimming",
        WorkoutActivityType.Walking => "Walking",
        WorkoutActivityType.Rowing => "Rowing",
        WorkoutActivityType.Elliptical => "Elliptical",
        WorkoutActivityType.Mobility => "Mobility",
        WorkoutActivityType.GentleRecovery => "Gentle recovery",
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unsupported scheduled activity.")
    };

    private static IReadOnlySet<DayOfWeek> ParseAvailableWeekdays(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<DayOfWeek>();
            return document.RootElement.EnumerateArray()
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String when Enum.TryParse<DayOfWeek>(value.GetString(), true, out var day) && Enum.IsDefined(day) => (DayOfWeek?)day,
                    JsonValueKind.Number when value.TryGetInt32(out var number) && Enum.IsDefined(typeof(DayOfWeek), number) => (DayOfWeek?)number,
                    _ => null
                })
                .Where(day => day.HasValue)
                .Select(day => day!.Value)
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<DayOfWeek>();
        }
    }

    private sealed record ProposalInputSnapshot(
        StrengthPlanPreviewRequestV020 Request,
        int ProfileVersion,
        Guid? BaselineActivePlanId,
        Guid? BaselineRevisionId);

    private sealed record ProposalSnapshot(
        string PolicyId,
        string PolicyVersion,
        string RulesetVersion,
        string Status,
        IReadOnlyList<StrengthPlanPreviewSessionV020> Sessions,
        MixedActivityScheduleV020? MixedActivitySchedule = null);

    private sealed record AcceptedPlanSnapshot(
        ProposalSnapshot Proposal,
        IReadOnlyList<StrengthWeekdayAssignment> WeekdayAssignments,
        IReadOnlyList<MixedActivityScheduleSessionV020> ScheduleSessions,
        int RepeatedWeeks);
}
