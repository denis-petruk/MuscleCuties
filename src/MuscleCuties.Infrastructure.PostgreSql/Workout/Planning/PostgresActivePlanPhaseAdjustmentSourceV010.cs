using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Builds a read-only phase review from owner-scoped PostgreSQL plan and cycle records.</summary>
public sealed class PostgresActivePlanPhaseAdjustmentSourceV010(
    PostgresDbContext dbContext,
    TimeProvider timeProvider) : IActivePlanPhaseAdjustmentSourceV010
{
    private const int NoSourceRecords = 0;
    private const int NoRecordedCompletion = 0;
    private const int FullCompletionPercent = 100;

    public async Task<ActivePlanPhaseAdjustmentSourceV010?> LoadAsync(Guid userId,
        string? requestedTimeZoneId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return null;

        await using var ownedTransaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        async Task CommitOwnedTransactionAsync()
        {
            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
            .ConfigureAwait(false);

        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var activePlan = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(plan => plan.UserId == userId && plan.IsActive)
            .OrderByDescending(plan => plan.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (profile is null || activePlan is null)
        {
            await CommitOwnedTransactionAsync().ConfigureAwait(false);
            return null;
        }

        var nowUtc = timeProvider.GetUtcNow();
        var recordedTimeZoneId = await dbContext.DailyReadinessLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.PlanningTimeZoneId != null)
            .OrderByDescending(log => log.PlanningRecordedAtUtc)
            .Select(log => log.PlanningTimeZoneId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var timeZone = TryFindTimeZone(requestedTimeZoneId) ??
                       TryFindTimeZone(recordedTimeZoneId) ?? TimeZoneInfo.Utc;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).DateTime);
        var dailyCheckIn = await dbContext.DailyReadinessLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.Date == localToday)
            .OrderByDescending(log => log.PlanningRevision)
            .ThenByDescending(log => log.PlanningRecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasActiveRestriction = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .AnyAsync(log => log.UserId == userId && log.Status != "Cleared", cancellationToken)
            .ConfigureAwait(false);
        var hasSymptomReport = await dbContext.SymptomLogs.AsNoTracking()
            .AnyAsync(log => log.UserId == userId && log.Date == localToday, cancellationToken)
            .ConfigureAwait(false);
        var hasCurrentSafetyReport = hasActiveRestriction || hasSymptomReport ||
                                     dailyCheckIn?.Pain is > NoRecordedCompletion ||
                                     dailyCheckIn?.Soreness is > NoRecordedCompletion;

        var baselineRevisionId = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(revision => revision.UserId == userId && revision.PlanId == activePlan.Id)
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => (Guid?)revision.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (baselineRevisionId is null)
        {
            await CommitOwnedTransactionAsync().ConfigureAwait(false);
            return null;
        }

        var phaseLog = await dbContext.CyclePhaseLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.LocalDate <= localToday)
            .OrderByDescending(log => log.LocalDate)
            .ThenByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var phaseContext = BuildPhaseContext(profile.CurrentCyclePhase, phaseLog);
        var baseline = ReadBaseline(profile.PhaseBaselinesJson, phaseContext.Phase);
        var baselineScale = IsValidBaseline(baseline)
            ? PhaseBaselineScale.QuizMappedOneToFive
            : PhaseBaselineScale.Unknown;

        var planDays = await dbContext.WorkoutDays.AsNoTracking()
            .Where(day => day.UserId == userId && day.WorkoutPlanId == activePlan.Id &&
                          !day.IsRemovedByReadiness && !day.IsRemovedByUserAdjustment && !day.IsRemovedBySafety &&
                          day.Date != null && day.Date >= localToday)
            .OrderBy(day => day.Date)
            .ThenBy(day => day.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var planEndDate = planDays.Where(day => day.Date != null)
            .Select(day => day.Date!.Value)
            .DefaultIfEmpty(localToday)
            .Max();
        var dayIds = planDays.Select(day => day.Id).ToArray();
        var exercises = dayIds.Length == NoSourceRecords
            ? []
            : await dbContext.WorkoutDayExercises.AsNoTracking()
                .Where(exercise => exercise.UserId == userId && dayIds.Contains(exercise.WorkoutDayId) &&
                                   !exercise.IsRemovedByReadiness)
                .OrderBy(exercise => exercise.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var workoutLogs = dayIds.Length == NoSourceRecords
            ? []
            : await dbContext.WorkoutLogs.AsNoTracking()
                .Where(log => log.UserId == userId && dayIds.Contains(log.WorkoutDayId))
                .OrderByDescending(log => log.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var workoutLogIds = workoutLogs.Select(item => item.Id).ToArray();
        var loggedExerciseIds = workoutLogIds.Length == NoSourceRecords
            ? new HashSet<Guid>()
            : (await dbContext.WorkoutExerciseLogs.AsNoTracking()
                    .Where(log => log.UserId == userId &&
                                  workoutLogIds.Contains(log.WorkoutLogId))
                    .Select(log => log.WorkoutDayExerciseId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet();
        var revision = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
            .SingleAsync(row => row.UserId == userId && row.Id == baselineRevisionId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        var acceptedPlanMap = ReadAcceptedPlanMap(revision.PlanJson);
        var sessions = BuildReviewSessions(planDays, exercises, activePlan.Name, acceptedPlanMap);
        var reviewSessions = planDays.Where(day => day.Date != null && !day.IsOptional &&
                                                  !day.IsEasierAlternative && IsWorkoutDay(day.WorkoutType))
            .Select(day =>
            {
                var date = day.Date!.Value;
                var dayLogs = workoutLogs.Where(log => log.WorkoutDayId == day.Id).ToArray();
                var hasStarted = dayLogs.Length > NoSourceRecords;
                var hasRecordedWork = dayLogs.Any(log => log.CompletionPercent > NoRecordedCompletion) ||
                                      exercises.Where(exercise => exercise.WorkoutDayId == day.Id)
                                          .Any(exercise => loggedExerciseIds.Contains(exercise.Id));
                var latestCompletion = dayLogs.OrderByDescending(log => log.CreatedAt)
                    .Select(log => (int?)log.CompletionPercent).FirstOrDefault();
                var state = day.IsRemovedByReadiness
                    ? PlannedSessionState.Skipped
                    : latestCompletion is >= FullCompletionPercent
                        ? PlannedSessionState.Completed
                        : latestCompletion is > NoRecordedCompletion
                            ? PlannedSessionState.PartiallyCompleted
                            : PlannedSessionState.Planned;
                var scheduledStart = new DateTimeOffset(
                    date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                return new PhaseReviewSession(day.Id, date, scheduledStart, state,
                    hasStarted, hasRecordedWork);
            })
            .ToArray();

        var gateInput = new PhaseReviewGateInput(activePlan.Id, baselineRevisionId.Value,
            planEndDate, nowUtc, localToday, phaseContext, baseline, baselineScale,
            PreviousReviewedPhase: null, PreviousReviewedContextId: null,
            PreviousReviewedBaseline: null, ExplicitReviewRequested: true,
            PhaseReviewPromptsEnabled: true, reviewSessions,
            new HashSet<PhaseReviewDismissalKey>(), hasCurrentSafetyReport);
        await CommitOwnedTransactionAsync().ConfigureAwait(false);
        return new ActivePlanPhaseAdjustmentSourceV010(gateInput, sessions, timeZone.Id);
    }

    private static TimeZoneInfo? TryFindTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static PhaseReviewContext BuildPhaseContext(string? profilePhase,
        CyclePhaseLogRecord? phaseLog)
    {
        if (phaseLog is not null &&
            Enum.TryParse<CyclePhase>(phaseLog.Phase, true, out var confirmedPhase) &&
            confirmedPhase != CyclePhase.Unknown && Enum.IsDefined(confirmedPhase))
            return new PhaseReviewContext(phaseLog.Id, confirmedPhase,
                PhaseReviewProvenance.UserConfirmed, null);

        if (Enum.TryParse<CyclePhase>(profilePhase, true, out var estimatedPhase) &&
            estimatedPhase != CyclePhase.Unknown && Enum.IsDefined(estimatedPhase))
            return new PhaseReviewContext(Guid.Empty, estimatedPhase,
                PhaseReviewProvenance.Estimated, null);

        return new PhaseReviewContext(Guid.Empty, CyclePhase.Unknown,
            PhaseReviewProvenance.Unknown, null);
    }

    private static PhaseBaseline? ReadBaseline(string json, CyclePhase phase)
    {
        if (string.IsNullOrWhiteSpace(json) || phase == CyclePhase.Unknown)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(phase.ToString(), out var baseline) ||
                !baseline.TryGetProperty("Pain", out var pain) ||
                !baseline.TryGetProperty("Energy", out var energy))
                return null;
            return new PhaseBaseline(pain.GetInt32(), energy.GetInt32());
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsValidBaseline(PhaseBaseline? baseline) => baseline is not null &&
        baseline.Pain is >= PhaseReviewRule.MinimumDiscomfortRating and
            <= PhaseReviewRule.MaximumDiscomfortRating &&
        baseline.Energy is >= PhaseReviewRule.MinimumEnergyRating and
            <= PhaseReviewRule.MaximumEnergyRating;

    private static IReadOnlyList<PhaseAdjustmentSession> BuildReviewSessions(
        IReadOnlyList<WorkoutDayRecord> planDays,
        IReadOnlyList<WorkoutDayExerciseRecord> exercises,
        string planName,
        AcceptedPlanReviewMap planMap)
    {
        return planDays.Where(day => day.Date != null && !day.IsOptional &&
                                     !day.IsEasierAlternative && IsWorkoutDay(day.WorkoutType))
            .Select(day =>
            {
                var dayExercises = exercises.Where(exercise => exercise.WorkoutDayId == day.Id).ToArray();
                var sessionIndex = ResolveSessionIndex(day, planMap.WeekdayBySessionIndex);
                var roles = sessionIndex is { } index && planMap.SlotRolesBySessionIndex
                    .TryGetValue(index, out var mappedRoles)
                    ? mappedRoles
                    : new Dictionary<int, (bool IsOptional, bool IsAccessory)>();
                var projectedExercises = dayExercises.Select(exercise =>
                {
                    roles.TryGetValue(exercise.ExerciseId, out var role);
                    return new PhaseAdjustmentExercise(exercise.Id, exercise.ExerciseId,
                        exercise.Sets, exercise.IsOptional && role.IsOptional,
                        exercise.IsOptional && role.IsAccessory);
                }).ToArray();
                var modality = ResolveModality(day.WorkoutType, day.Name,
                    projectedExercises.Length > NoSourceRecords);
                var style = modality == ActivityModality.Strength
                    ? TrainingStyle(planName)
                    : modality == ActivityModality.Other
                        ? SessionTrainingStyle.Unspecified
                        : day.Name.Contains("hiit", StringComparison.OrdinalIgnoreCase)
                            ? SessionTrainingStyle.Hiit
                            : SessionTrainingStyle.Continuous;
                return new PhaseAdjustmentSession(day.Id, modality,
                    style,
                    projectedExercises,
                    modality == ActivityModality.Strength ? null : day.PlannedMainDurationMinutes,
                    ExistingTemplateOptions: ExistingTemplateOptions(day, planDays));
            }).ToArray();
    }

    private static IReadOnlyList<PhaseAdjustmentTemplateOption> ExistingTemplateOptions(
        WorkoutDayRecord target,
        IReadOnlyList<WorkoutDayRecord> planDays)
    {
        var options = new List<PhaseAdjustmentTemplateOption>();
        foreach (var candidate in planDays.Where(day => day.Id != target.Id && day.IsOptional &&
                     day.IsEasierAlternative && day.Date == target.Date))
        {
            var templateChoice = IsGentleTemplate(candidate)
                ? PhaseAdjustmentChoice.SelectEligibleGentleTemplate
                : IsEasyCardioTemplate(candidate)
                    ? PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate
                    : (PhaseAdjustmentChoice?)null;
            if (templateChoice is { } choice)
                options.Add(new PhaseAdjustmentTemplateOption(choice, candidate.Id.ToString("D"), candidate.Name));
        }
        return Array.AsReadOnly(options.ToArray());
    }

    private static bool IsGentleTemplate(WorkoutDayRecord day) =>
        IsGentleTemplate(day.WorkoutType, day.Name);

    private static bool IsGentleTemplate(string workoutType, string name) =>
        string.Equals(workoutType, nameof(WorkoutType.Recovery), StringComparison.OrdinalIgnoreCase) ||
        name.Contains("gentle", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("mobility", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("yoga", StringComparison.OrdinalIgnoreCase);

    private static bool IsEasyCardioTemplate(WorkoutDayRecord day) =>
        string.Equals(day.WorkoutType, nameof(WorkoutType.Cardio), StringComparison.OrdinalIgnoreCase) &&
        (day.Name.Contains("easy", StringComparison.OrdinalIgnoreCase) ||
         day.Name.Contains("walk", StringComparison.OrdinalIgnoreCase));

    private static ActivityModality ResolveModality(string workoutType, string name,
        bool hasStrengthExercises)
    {
        if (hasStrengthExercises || string.Equals(workoutType, nameof(WorkoutType.Strength),
                StringComparison.OrdinalIgnoreCase))
            return ActivityModality.Strength;
        if (string.Equals(workoutType, nameof(WorkoutType.Cardio), StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("walk", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Walking;
            if (name.Contains("run", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Running;
            if (name.Contains("cycling", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("bike", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("hiit", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Cycling;
            if (name.Contains("swim", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Swimming;
            if (name.Contains("row", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Rowing;
            if (name.Contains("elliptical", StringComparison.OrdinalIgnoreCase))
                return ActivityModality.Elliptical;
        }
        if (IsGentleTemplate(workoutType, name))
            return ActivityModality.Mobility;
        return ActivityModality.Other;
    }

    private static int? ResolveSessionIndex(WorkoutDayRecord day,
        IReadOnlyDictionary<int, DayOfWeek> weekdayBySessionIndex)
    {
        foreach (var assignment in weekdayBySessionIndex)
        {
            if ((int)assignment.Value == day.DayOfWeek)
                return assignment.Key;
        }
        return null;
    }

    private static AcceptedPlanReviewMap ReadAcceptedPlanMap(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("proposal", out var proposal) ||
                !proposal.TryGetProperty("sessions", out var sessions) ||
                sessions.ValueKind != JsonValueKind.Array ||
                !document.RootElement.TryGetProperty("weekdayAssignments", out var assignments) ||
                assignments.ValueKind != JsonValueKind.Array)
                return AcceptedPlanReviewMap.Empty;

            var rolesBySession = new Dictionary<int, IReadOnlyDictionary<int, (bool, bool)>>();
            foreach (var session in sessions.EnumerateArray())
            {
                if (!session.TryGetProperty("sessionIndex", out var sessionIndexElement) ||
                    !sessionIndexElement.TryGetInt32(out var sessionIndex) ||
                    !session.TryGetProperty("slots", out var slots) ||
                    slots.ValueKind != JsonValueKind.Array)
                    continue;
                var roles = new Dictionary<int, (bool IsOptional, bool IsAccessory)>();
                foreach (var slot in slots.EnumerateArray())
                {
                    if (!slot.TryGetProperty("exerciseId", out var exerciseElement) ||
                        !exerciseElement.TryGetInt32(out var exerciseId))
                        continue;
                    if (!slot.TryGetProperty("isRequired", out var requiredElement) ||
                        requiredElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        continue;
                    var isRequired = requiredElement.GetBoolean();
                    if (
                        !slot.TryGetProperty("prescriptionRole", out var roleElement) ||
                        !roleElement.TryGetInt32(out var roleValue))
                        continue;
                    var isAccessory = roleValue == (int)StrengthPrescriptionRole.Accessory;
                    roles[exerciseId] = (!isRequired, isAccessory);
                }
                rolesBySession[sessionIndex] = roles;
            }

            var weekdayBySession = new Dictionary<int, DayOfWeek>();
            foreach (var assignment in assignments.EnumerateArray())
            {
                if (!assignment.TryGetProperty("sessionIndex", out var sessionIndexElement) ||
                    !sessionIndexElement.TryGetInt32(out var sessionIndex) ||
                    !assignment.TryGetProperty("weekday", out var weekdayElement) ||
                    !weekdayElement.TryGetInt32(out var weekdayValue) ||
                    !Enum.IsDefined(typeof(DayOfWeek), weekdayValue))
                    continue;
                weekdayBySession[sessionIndex] = (DayOfWeek)weekdayValue;
            }
            return new AcceptedPlanReviewMap(weekdayBySession, rolesBySession);
        }
        catch (JsonException)
        {
            return AcceptedPlanReviewMap.Empty;
        }
    }

    private static bool IsWorkoutDay(string workoutType) =>
        !string.Equals(workoutType, nameof(WorkoutType.Rest), StringComparison.OrdinalIgnoreCase);

    private static SessionTrainingStyle TrainingStyle(string planName) =>
        planName.Contains("high volume", StringComparison.OrdinalIgnoreCase)
            ? SessionTrainingStyle.HighVolume
            : planName.Contains("high intensity", StringComparison.OrdinalIgnoreCase)
                ? SessionTrainingStyle.HighIntensity
                : SessionTrainingStyle.General;

    private sealed record AcceptedPlanReviewMap(
        IReadOnlyDictionary<int, DayOfWeek> WeekdayBySessionIndex,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, (bool IsOptional, bool IsAccessory)>> SlotRolesBySessionIndex)
    {
        public static AcceptedPlanReviewMap Empty { get; } = new(
            new Dictionary<int, DayOfWeek>(),
            new Dictionary<int, IReadOnlyDictionary<int, (bool, bool)>>());
    }
}
