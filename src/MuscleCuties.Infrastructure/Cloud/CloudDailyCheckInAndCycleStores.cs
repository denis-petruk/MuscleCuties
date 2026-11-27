using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.History;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The daily check-in through the backend: whether today is already answered, and the answer itself.</summary>
public sealed class CloudDailyCheckInStore(
    IMuscleCutiesBackendApiClient apiClient,
    MuscleCuties.Core.Services.Health.IHealthSyncService? healthSync = null,
    MuscleCuties.Core.Services.Auth.IAuthService? authService = null,
    TimeProvider? clock = null) : IDailyCheckInStore
{
    private const string CheckInsRoute = "/api/v1/check-ins";
    private const int MinimumReadinessScore = 0;
    private const int MaximumReadinessScore = 100;
    private const string UnknownScoreText = "—";
    private readonly TimeProvider timeProvider = clock ?? TimeProvider.System;

    /// <summary>Today's check-in state, so the sheet can close itself instead of asking the same questions twice.</summary>
    public async Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct)
    {
        var response = await apiClient.GetAsync<DailyCheckInRecord[]>(
            $"{CheckInsRoute}?from={request.Date:yyyy-MM-dd}&through={request.Date:yyyy-MM-dd}",
            ct).ConfigureAwait(false);

        var records = CloudStoreResponse.RequireData(response, "daily check-in status");
        if (records.Length == 0)
            return new DailyCheckInStatusResponse(false, string.Empty, string.Empty);

        var latest = records[^1];
        var readiness = TodayReadinessCheckInMapper.Map(latest.PlanningSleepQuality, latest.Energy,
            latest.PlanningSoreness, latest.PlanningStress, latest.Date, latest.PlanningTimeZoneId,
            latest.PlanningRecordedAtUtc, latest.PlanningRevision, timeProvider.GetUtcNow(), request.TimeZoneId,
            latest.PlanningFatigue);
        return new DailyCheckInStatusResponse(readiness.Status == "Available",
            readiness.RawScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? UnknownScoreText,
            readiness.Band ?? readiness.Status, readiness);
    }

    /// <summary>Submits the day's answers once and returns what the readiness calculation made of them.</summary>
    public async Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct)
    {
        if (request.EnergyLevel is < 1 or > 5 || request.PlanningSleepQuality is not (>= 1 and <= 5) ||
            request.PlanningFatigue is not (>= 1 and <= 5) ||
            request.PlanningSoreness is not (null or >= 1 and <= 5) ||
            request.PlanningStress is not (null or >= 1 and <= 5) ||
            !CheckInObservationRule.IsValidTimeZoneId(request.PlanningTimeZoneId))
            throw new ArgumentException("Energy, fatigue, sleep quality and a valid local time zone are required.", nameof(request));
        var localZone = TimeZoneInfo.FindSystemTimeZoneById(request.PlanningTimeZoneId!);
        var todayInLocalZone = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), localZone).DateTime);
        if (request.Date != todayInLocalZone)
            throw new ArgumentException("Daily check-in must be saved for today in the current local time zone.", nameof(request));

        MuscleCuties.Core.Services.Health.HealthWeeklySummary? healthSummary = null;
        if (healthSync is not null && authService is not null)
        {
            var userId = await authService.GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId > 0)
            {
                healthSummary = await healthSync.GetCachedWeeklySummaryAsync(userId).ConfigureAwait(false);
            }
        }

        var existingResponse = await apiClient.GetAsync<DailyCheckInRecord[]>(
            $"{CheckInsRoute}?from={request.Date:yyyy-MM-dd}&through={request.Date:yyyy-MM-dd}",
            ct).ConfigureAwait(false);
        var existingRecords = CloudStoreResponse.RequireData(existingResponse, "load daily check-in before save");
        var checkInId = existingRecords.Length > 0 && existingRecords[^1].Id != Guid.Empty
            ? existingRecords[^1].Id
            : Guid.CreateVersion7();

        // The check-in view model is the single pre-fill owner; what is saved equals what the user saw and confirmed.
        var effectiveSleepHours = request.SleepHours;

        var body = new
        {
            id = checkInId,
            date = request.Date,
            sleepHours = effectiveSleepHours is { } hours ? (decimal?)hours : null,
            stepsYesterday = healthSummary?.YesterdaySteps ?? (healthSummary?.HasMovementData == true ? healthSummary.AverageSteps : (int?)null),
            energy = request.EnergyLevel,
            pain = request.PainLevel,
            bloating = request.HasBloating,
            weightKg = request.BodyWeightKg.HasValue ? (decimal?)request.BodyWeightKg.Value : null,
            sleep3dAvg = healthSummary?.AverageSleep3dHours is > 0 ? (decimal?)healthSummary.AverageSleep3dHours.Value : null,
            steps7dAvg = healthSummary?.HasMovementData == true ? (int?)healthSummary.AverageSteps : null,
            planningSleepQuality = request.PlanningSleepQuality,
            planningFatigue = request.PlanningFatigue,
            planningSoreness = request.PlanningSoreness,
            planningStress = request.PlanningStress,
            planningTimeZoneId = request.PlanningTimeZoneId
        };

        var response = await apiClient.SendMutationAsync<object, DailyCheckInRecord>(
            HttpMethod.Put,
            CheckInsRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);

        var saved = CloudStoreResponse.RequireData(response, "submit daily check-in");
        if (saved.Id != checkInId || saved.Date != request.Date ||
            saved.ReadinessScore is < MinimumReadinessScore or > MaximumReadinessScore ||
            saved.PlanningFatigue != request.PlanningFatigue)
            throw new InvalidOperationException("Cloud submit daily check-in returned an invalid record.");
        var readiness = TodayReadinessCheckInMapper.Map(saved.PlanningSleepQuality, saved.Energy,
            saved.PlanningSoreness, saved.PlanningStress, saved.Date, saved.PlanningTimeZoneId,
            saved.PlanningRecordedAtUtc, saved.PlanningRevision, timeProvider.GetUtcNow(),
            request.PlanningTimeZoneId, saved.PlanningFatigue);
        if (readiness.Status != "Available")
            throw new InvalidOperationException("The saved check-in did not contain a valid score for today.");
        return new SubmitDailyCheckInResponse(saved.ReadinessScore,
            readiness.Band ?? readiness.Status, string.Empty, false, readiness);
    }

}

/// <summary>Injuries through the backend: how they restrict training, and the writes that add, clear or remove one.</summary>
public sealed class CloudWorkoutInjuryStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutInjuryStore
{
    private const string InjuriesRoute = "/api/v1/injuries";
    private const string InjuryImpactRoute = "/api/v1/workouts/injuries/impact";
    private const string ReconcileRoute = "/api/v1/workouts/plan/reconcile";
    private readonly Dictionary<Guid, int> _intIdByGuid = [];
    private readonly Dictionary<int, InjuryObservationRecord> _recordByIntId = [];
    private readonly object _identityLock = new();

    /// <summary>How the logged injuries restrict today's training, which is what withholds an unsafe session.</summary>
    public async Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default)
    {
        var response = await apiClient.GetAsync<WorkoutInjuryImpactDto>(InjuryImpactRoute, ct).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "workout injury impact");
    }

    /// <summary>Every logged injury.</summary>
    public async Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default)
    {
        var response = await apiClient.GetAsync<InjuryObservationRecord[]>(InjuriesRoute, ct).ConfigureAwait(false);
        var records = CloudStoreResponse.RequireData(response, "workout injuries");
        var list = new List<WorkoutInjuryLogDto>(records.Length);
        foreach (var record in records)
        {
            var intId = CacheRecord(record);
            var site = Enum.IsDefined(typeof(WorkoutInjurySite), record.SiteFlag)
                ? (WorkoutInjurySite)record.SiteFlag
                : WorkoutInjurySite.None;
            var status = Enum.TryParse<WorkoutInjuryStatus>(record.Status, true, out var parsedStatus) &&
                         Enum.IsDefined(parsedStatus)
                ? parsedStatus
                : WorkoutInjuryStatus.Acute;

            list.Add(new WorkoutInjuryLogDto(
                intId,
                site,
                status,
                record.Pain,
                record.Since,
                record.Date,
                record.IsActive && status != WorkoutInjuryStatus.Cleared,
                record.WorseningTrigger,
                record.ClinicianAdvisedAvoid, record.PainReassessmentDueAtUtc));
        }

        return list;
    }

    /// <summary>Saves one injury report and refreshes the cached copies the summary reads.</summary>
    public async Task SaveInjuryAsync(SaveWorkoutInjuryInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var injuryGuid = Guid.CreateVersion7();
        var loggedDate = DateOnly.FromDateTime(DateTime.Today);
        if (input.Id > 0)
        {
            var existing = await TryResolveRecordAsync(input.Id, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                injuryGuid = existing.Id;
                loggedDate = existing.Date;
            }
        }

        var body = new
        {
            id = injuryGuid,
            siteFlag = (int)input.Site,
            status = input.Status.ToString(),
            since = input.Since,
            pain = input.Pain,
            date = loggedDate,
            worseningTrigger = input.WorseningTrigger.ToString(),
            clinicianAdvisedAvoid = input.ClinicianAdvisedAvoid
        };

        var response = await apiClient.SendMutationAsync<object, InjuryObservationRecord>(
            HttpMethod.Put,
            InjuriesRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);
        if (response.Status == BackendApiStatus.Conflict &&
            response.ErrorCode == "ActiveWorkoutPreventsInjury")
            throw new ActiveWorkoutPreventsInjuryException();
        var saved = CloudStoreResponse.RequireData(response, "save workout injury");
        CacheRecord(saved);
    }

    /// <summary>Marks an injury as healed without deleting its history, and refreshes the plan afterwards.</summary>
    public async Task ClearInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        var existing = await RequireRecordAsync(injuryId, ct).ConfigureAwait(false);
        // Clearing an injury must not erase the safety answers the user already gave.
        var body = new
        {
            id = existing.Id,
            siteFlag = existing.SiteFlag,
            status = WorkoutInjuryStatus.Cleared.ToString(),
            since = existing.Since,
            pain = existing.Pain,
            date = existing.Date,
            worseningTrigger = existing.WorseningTrigger.ToString(),
            clinicianAdvisedAvoid = existing.ClinicianAdvisedAvoid
        };

        var response = await apiClient.SendMutationAsync<object, InjuryObservationRecord>(
            HttpMethod.Put,
            InjuriesRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);
        var updated = CloudStoreResponse.RequireData(response, "clear workout injury");
        CacheRecord(updated);
    }

    /// <summary>Removes an injury report outright and refreshes the plan afterwards.</summary>
    public async Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        var existing = await RequireRecordAsync(injuryId, ct).ConfigureAwait(false);
        var response = await apiClient.DeleteAsync($"{InjuriesRoute}/{existing.Id:D}", null, ct).ConfigureAwait(false);
        CloudStoreResponse.RequireSuccess(response, "delete workout injury");

        lock (_identityLock)
        {
            _recordByIntId.Remove(injuryId);
            _intIdByGuid.Remove(existing.Id);
        }
    }

    /// <summary>Asks for a plan refresh after an injury changed, because a restriction that is no longer true must not keep</summary>
    /// withholding sessions.</summary>
    public async Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default)
    {
        var response = await apiClient.SendMutationAsync<object, WorkoutReconciliationResponse>(
            HttpMethod.Post, ReconcileRoute, new { },
            idempotencyKey: Guid.NewGuid().ToString("N"), ct).ConfigureAwait(false);
        // F08: NeedsReview is a successful reconcile that flags days for review, not a failure.
        // Throwing here made the injury screen claim the plan was not updated after it had been.
        CloudStoreResponse.RequireData(response, "refresh workout plan after injury change");
    }

    /// <summary>The stored record behind an injury, or nothing when it has gone.</summary>
    private async Task<InjuryObservationRecord?> TryResolveRecordAsync(int injuryId, CancellationToken ct)
    {
        lock (_identityLock)
        {
            if (_recordByIntId.TryGetValue(injuryId, out var cached))
                return cached;
        }

        await GetAllInjuriesAsync(ct).ConfigureAwait(false);
        lock (_identityLock)
        {
            return _recordByIntId.TryGetValue(injuryId, out var resolved) ? resolved : null;
        }
    }

    /// <summary>Loads the injury record or refuses because it is unknown.</summary>
    private async Task<InjuryObservationRecord> RequireRecordAsync(int injuryId, CancellationToken ct) =>
        await TryResolveRecordAsync(injuryId, ct).ConfigureAwait(false)
        ?? throw new KeyNotFoundException($"Injury {injuryId} was not found.");

    /// <summary>Keeps the saved record in the local cache and returns its identity, so the page can address it afterwards.</summary>
    private int CacheRecord(InjuryObservationRecord record)
    {
        lock (_identityLock)
        {
            if (!_intIdByGuid.TryGetValue(record.Id, out var intId))
            {
                intId = StableIntId.From(record.Id);
                while (_recordByIntId.TryGetValue(intId, out var existing) && existing.Id != record.Id)
                {
                    intId = intId == int.MaxValue ? 1 : intId + 1;
                }

                _intIdByGuid[record.Id] = intId;
            }

            _recordByIntId[intId] = record;
            return intId;
        }
    }

}

/// <summary>The cycle day log through the backend: the day's phase, flow, symptoms and note.</summary>
public sealed class CloudCycleDailyLogStore(
    IMuscleCutiesBackendApiClient apiClient) : ICycleDailyLogStore
{
    private const string CycleOverviewRoute = "/api/v1/cycle/overview";
    private const string CycleHistoryRoute = "/api/v1/cycle/history";
    private const string CyclePhasesRoute = "/api/v1/cycle/history/phases";
    private const string PeriodStartedNote = "Period started";
    private const string PeriodEndedNote = "Period ended";
    private const int DefaultSymptomSeverity = 2;
    private const int CycleLookbackDays = 60;
    private const int GuidByteCount = 16;

    /// <summary>One day's log, or nothing when that day was never logged.</summary>
    public async Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken)
    {
        var dateOnly = DateOnly.FromDateTime(date);
        var response = await apiClient.GetAsync<ServerCycleOverview>(
            $"{CycleOverviewRoute}?from={dateOnly:yyyy-MM-dd}&through={dateOnly:yyyy-MM-dd}&today={dateOnly:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);

        var overview = CloudStoreResponse.RequireData(response, "cycle daily log");
        var phaseLog = overview.PhaseLogs
            .Where(log => DateOnly.FromDateTime(log.LoggedAt) == dateOnly)
            .OrderBy(log => log.CreatedAt)
            .LastOrDefault();
        var daySymptoms = overview.History.Symptoms
            .Where(symptom => symptom.Date == dateOnly)
            .Select(symptom => Enum.TryParse<SymptomTypeDto>(symptom.Type, true, out var parsed) && Enum.IsDefined(parsed)
                ? (SymptomTypeDto?)parsed
                : null)
            .Where(symptom => symptom.HasValue)
            .Select(symptom => symptom!.Value)
            .Distinct()
            .ToArray();

        var cycleStartedOnDate = overview.History.Cycles.Any(cycle => cycle.StartDate == dateOnly);
        var cycleEndedOnDate = overview.History.Cycles.Any(cycle => cycle.EndDate == dateOnly);

        if (phaseLog is null && daySymptoms.Length == 0 && !cycleStartedOnDate && !cycleEndedOnDate)
            return null;

        FlowIntensityDto? flow = null;
        if (!string.IsNullOrWhiteSpace(phaseLog?.FlowIntensityValue) &&
            Enum.TryParse<FlowIntensityDto>(phaseLog.FlowIntensityValue, true, out var parsedFlow) &&
            Enum.IsDefined(parsedFlow))
        {
            flow = parsedFlow;
        }

        var isPeriodStarted = string.Equals(phaseLog?.Note, PeriodStartedNote, StringComparison.OrdinalIgnoreCase) ||
                              cycleStartedOnDate;
        var isPeriodEnded = string.Equals(phaseLog?.Note, PeriodEndedNote, StringComparison.OrdinalIgnoreCase) ||
                            cycleEndedOnDate;

        return new CycleDailyLogSnapshot(
            date.Date,
            isPeriodStarted,
            isPeriodEnded,
            flow,
            daySymptoms);
    }

    /// <summary>Writes a day's log with a deterministic request id, so a retried save is the same operation; a phase that</summary>
    /// breaks the cycle order is refused with the phase that would have been accepted.</summary>
    public async Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dateOnly = DateOnly.FromDateTime(request.Date);

        var targetPhase = ResolveTargetPhase(request);
        if (targetPhase.HasValue)
        {
            var note = !string.IsNullOrWhiteSpace(request.Note)
                ? request.Note
                : request.IsPeriodStart == true
                    ? PeriodStartedNote
                    : request.IsPeriodEnd == true
                        ? PeriodEndedNote
                        : null;

            var phaseResponse = await apiClient.SendMutationAsync<object, CloudPhaseLogSaveResponse>(
                HttpMethod.Put,
                CyclePhasesRoute,
                new
                {
                    localDate = dateOnly,
                    phase = targetPhase.Value.ToString(),
                    flowIntensity = request.FlowIntensity?.ToString(),
                    note
                },
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (phaseResponse.Status == BackendApiStatus.Conflict)
                ThrowPhaseOrderOrConflict(phaseResponse.ErrorCode);

            RequireSavedPhaseLog(phaseResponse);
        }

        var lookbackStart = dateOnly.AddDays(-CycleLookbackDays);
        var historyResponse = await apiClient.GetAsync<CycleHistorySnapshot>(
            $"{CycleHistoryRoute}?from={lookbackStart:yyyy-MM-dd}&through={dateOnly:yyyy-MM-dd}",
            cancellationToken).ConfigureAwait(false);
        var history = CloudStoreResponse.RequireData(historyResponse, "cycle history for daily log");

        var activeCycle = history.Cycles
            .Where(cycle => cycle.StartDate <= dateOnly && (cycle.EndDate is null || cycle.EndDate >= dateOnly))
            .OrderByDescending(cycle => cycle.StartDate)
            .FirstOrDefault();

        if (request.IsPeriodEnd == true && activeCycle is not null && activeCycle.StartDate <= dateOnly && activeCycle.EndDate != dateOnly)
        {
            var updateCycleResponse = await apiClient.SendMutationAsync<object, CycleObservation>(
                HttpMethod.Put,
                $"{CycleHistoryRoute}/cycles",
                new
                {
                    id = activeCycle.Id,
                    startDate = activeCycle.StartDate,
                    endDate = (DateOnly?)dateOnly
                },
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);
            activeCycle = CloudStoreResponse.RequireData(updateCycleResponse, "save period end");
        }

        var existingDaySymptoms = history.Symptoms.Where(symptom => symptom.Date == dateOnly).ToArray();
        var requestedSymptoms = (request.Symptoms ?? []).Distinct().ToArray();

        if (requestedSymptoms.Length > 0 && activeCycle is null)
            throw new CycleSymptomsNeedActiveCycleIssue(
                "Record a period start before adding symptoms without a known cycle phase.");

        var requestedNames = requestedSymptoms.Select(symptom => symptom.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in existingDaySymptoms)
        {
            if (!requestedNames.Contains(existing.Type))
            {
                var deleteResponse = await apiClient.DeleteAsync(
                    $"{CycleHistoryRoute}/symptoms/{existing.Id:D}",
                    null,
                    cancellationToken).ConfigureAwait(false);
                CloudStoreResponse.RequireSuccess(deleteResponse, "delete cycle symptom");
            }
        }

        if (activeCycle is not null)
        {
            foreach (var symptom in requestedSymptoms)
            {
                var existing = existingDaySymptoms.FirstOrDefault(
                    item => string.Equals(item.Type, symptom.ToString(), StringComparison.OrdinalIgnoreCase));
                var symptomId = existing?.Id ?? DeterministicGuid($"symptom:{activeCycle.Id:N}:{dateOnly:yyyy-MM-dd}:{symptom}");
                var saveSymptomResponse = await apiClient.SendMutationAsync<object, CycleSymptom>(
                    HttpMethod.Put,
                    $"{CycleHistoryRoute}/symptoms",
                    new
                    {
                        id = symptomId,
                        cycleId = activeCycle.Id,
                        date = dateOnly,
                        type = symptom.ToString(),
                        severity = existing?.Severity ?? DefaultSymptomSeverity,
                        notes = request.Note ?? existing?.Notes
                    },
                    idempotencyKey: Guid.NewGuid().ToString("N"),
                    cancellationToken).ConfigureAwait(false);
                CloudStoreResponse.RequireData(saveSymptomResponse, "save cycle symptom");
            }
        }

        return true;
    }

    /// <summary>The phase a save should record, which is the requested one or the day's existing phase when none was sent.</summary>
    private static CyclePhaseDto? ResolveTargetPhase(CycleDailyLogRequest request)
    {
        if (request.PhaseOverride.HasValue && request.PhaseOverride.Value != CyclePhaseDto.Unknown)
            return request.PhaseOverride.Value;
        if (request.IsPeriodStart == true ||
            (request.FlowIntensity.HasValue && request.FlowIntensity.Value != FlowIntensityDto.None))
            return CyclePhaseDto.Menstrual;
        if (request.IsPeriodEnd == true)
            return CyclePhaseDto.Follicular;
        return null;
    }

    /// <summary>The suggested phase a refused save carries, so the page can offer it instead of only reporting the refusal.</summary>
    internal static bool TryParseSuggestedPhase(string? errorCode, out CyclePhaseDto suggestedPhase)
    {
        suggestedPhase = default;
        if (string.IsNullOrWhiteSpace(errorCode) ||
            !Enum.TryParse<CyclePhaseDto>(errorCode, true, out var parsed) ||
            !Enum.IsDefined(parsed) || parsed == CyclePhaseDto.Unknown)
            return false;

        suggestedPhase = parsed;
        return true;
    }

    /// <summary>Turns the server's order-error code into the order exception the use case expects, rather than a generic</summary>
    /// failure.</summary>
    internal static void ThrowPhaseOrderOrConflict(string? errorCode)
    {
        if (TryParseSuggestedPhase(errorCode, out var suggestedPhase))
            throw new CyclePhaseOrderIssue("This phase would skip a step.", suggestedPhase);

        throw new InvalidOperationException("Cycle phase update conflicted with existing cycle records.");
    }

    /// <summary>A stable id derived from a key, which is how the same logical write keeps one identity across retries.</summary>
    private static Guid DeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, GuidByteCount));
    }

    /// <summary>Throws unless the phase log really was stored, so a silently refused write cannot look like success.</summary>
    internal static void RequireSavedPhaseLog(BackendApiResponse<CloudPhaseLogSaveResponse> response)
    {
        var saved = CloudStoreResponse.RequireData(response, "save cycle phase");
        if (!string.Equals(saved.Outcome, "Saved", StringComparison.Ordinal))
            throw new InvalidOperationException("Cloud save cycle phase returned an invalid outcome.");
    }
}

/// <summary>The phase write's outcome as the server reports it, including which phase it would have accepted.</summary>
internal sealed record CloudPhaseLogSaveResponse(string? Outcome);

/// <summary>Edits to a single logged phase, kept apart from the day log because they change history rather than today.</summary>
public sealed class CloudCyclePhaseEditStore(
    IMuscleCutiesBackendApiClient apiClient) : ICyclePhaseEditStore
{
    private const string CyclePhasesRoute = "/api/v1/cycle/history/phases";
    private const string SavedOutcome = "Saved";

    /// <summary>Saves one phase correction and reports the outcome, including an order conflict with its suggestion.</summary>
    public async Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new
        {
            localDate = DateOnly.FromDateTime(request.Date),
            phase = request.Phase.ToString(),
            flowIntensity = (string?)null,
            note = request.Note
        };

        var response = await apiClient.SendMutationAsync<object, CloudPhaseLogSaveResponse>(
            HttpMethod.Put,
            CyclePhasesRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status switch
        {
            BackendApiStatus.Success when string.Equals(response.Data?.Outcome, SavedOutcome, StringComparison.Ordinal)
                => CyclePhaseEditResult.Saved,
            BackendApiStatus.Conflict
                when CloudCycleDailyLogStore.TryParseSuggestedPhase(response.ErrorCode, out var suggestedPhase)
                => CyclePhaseEditResult.OrderConflict(suggestedPhase),
            BackendApiStatus.ValidationFailed => CyclePhaseEditResult.InvalidInput,
            _ => CyclePhaseEditResult.Unavailable
        };
    }
}
