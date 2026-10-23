using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.History;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudDailyCheckInStore(
    IMuscleCutiesBackendApiClient apiClient,
    MuscleCuties.Core.Services.Health.IHealthSyncService? healthSync = null,
    MuscleCuties.Core.Services.Auth.IAuthService? authService = null) : IDailyCheckInStore
{
    private const string CheckInsRoute = "/api/v1/check-ins";
    private const int MinimumReadinessScore = 0;
    private const int MaximumReadinessScore = 100;
    private const string UnknownScoreText = "—";
    private const string FallbackCompletedTierText = "Check-in complete";

    public async Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct)
    {
        var response = await apiClient.GetAsync<DailyCheckInRecord[]>(
            $"{CheckInsRoute}?from={request.Date:yyyy-MM-dd}&through={request.Date:yyyy-MM-dd}",
            ct).ConfigureAwait(false);

        var records = CloudStoreResponse.RequireData(response, "daily check-in status");
        if (records.Length == 0)
            return new DailyCheckInStatusResponse(false, string.Empty, string.Empty);

        var latest = records[^1];
        var score = latest.ReadinessScore is >= MinimumReadinessScore and <= MaximumReadinessScore
            ? latest.ReadinessScore.ToString(CultureInfo.InvariantCulture)
            : UnknownScoreText;
        var tier = FormatReadinessTier(latest.ReadinessTier);
        return new DailyCheckInStatusResponse(true, score, tier);
    }

    public async Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct)
    {
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

        var effectiveSleepHours = request.SleepHours > 0
            ? request.SleepHours
            : (healthSummary?.LastNightSleepHours is > 0 ? healthSummary.LastNightSleepHours.Value : request.SleepHours);

        var body = new
        {
            id = checkInId,
            date = request.Date,
            sleepHours = (decimal)effectiveSleepHours,
            stepsYesterday = healthSummary?.YesterdaySteps ?? (healthSummary?.HasMovementData == true ? healthSummary.AverageSteps : (int?)null),
            energy = request.EnergyLevel,
            pain = request.PainLevel,
            bloating = request.HasBloating,
            weightKg = request.BodyWeightKg.HasValue ? (decimal?)request.BodyWeightKg.Value : null,
            sleep3dAvg = healthSummary?.AverageSleep3dHours is > 0 ? (decimal?)healthSummary.AverageSleep3dHours.Value : null,
            steps7dAvg = healthSummary?.HasMovementData == true ? (int?)healthSummary.AverageSteps : null
        };

        var response = await apiClient.SendMutationAsync<object, DailyCheckInRecord>(
            HttpMethod.Put,
            CheckInsRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);

        var saved = CloudStoreResponse.RequireData(response, "submit daily check-in");
        if (saved.Id != checkInId || saved.Date != request.Date ||
            saved.ReadinessScore is < MinimumReadinessScore or > MaximumReadinessScore)
            throw new InvalidOperationException("Cloud submit daily check-in returned an invalid record.");
        var tier = FormatReadinessTier(saved.ReadinessTier);

        var workoutResponse = await apiClient.GetAsync<WorkoutOverviewResponse>(
            $"/api/v1/workouts/overview?startDate={request.Date:yyyy-MM-dd}",
            ct).ConfigureAwait(false);
        if (workoutResponse.Status == BackendApiStatus.Success && workoutResponse.Data is not null)
        {
            var todaySchedule = workoutResponse.Data.Schedule.FirstOrDefault(s => s.IsToday);
            var todayWorkout = todaySchedule is not null
                ? workoutResponse.Data.Workouts.FirstOrDefault(w => w.WorkoutDayId == todaySchedule.WorkoutDayId)
                : workoutResponse.Data.Workouts.FirstOrDefault();
            return new SubmitDailyCheckInResponse(
                saved.ReadinessScore,
                tier,
                todaySchedule?.Title ?? todayWorkout?.Title ?? workoutResponse.Data.PlanName ?? string.Empty,
                false);
        }

        return new SubmitDailyCheckInResponse(saved.ReadinessScore, tier, string.Empty, true);
    }

    private static string FormatReadinessTier(string? rawTier)
    {
        if (string.IsNullOrWhiteSpace(rawTier))
            return FallbackCompletedTierText;

        if (int.TryParse(rawTier, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericTier) &&
            Enum.IsDefined(typeof(ReadinessTier), numericTier))
            return ((ReadinessTier)numericTier).ToString();

        if (Enum.TryParse<ReadinessTier>(rawTier, true, out var parsedTier) && Enum.IsDefined(parsedTier))
            return parsedTier.ToString();

        return rawTier;
    }
}

public sealed class CloudWorkoutInjuryStore(
    IMuscleCutiesBackendApiClient apiClient) : IWorkoutInjuryStore
{
    private const string InjuriesRoute = "/api/v1/injuries";
    private const string InjuryImpactRoute = "/api/v1/workouts/injuries/impact";
    private const string ProfileRoute = "/api/v1/profile";
    private const string WorkoutPreferencesRoute = "/api/v1/profile/workout-preferences";
    private const int DefaultWorkoutDaysPerWeek = 3;
    private readonly Dictionary<Guid, int> _intIdByGuid = [];
    private readonly Dictionary<int, InjuryObservationRecord> _recordByIntId = [];
    private readonly object _identityLock = new();

    public async Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default)
    {
        var response = await apiClient.GetAsync<WorkoutInjuryImpactDto>(InjuryImpactRoute, ct).ConfigureAwait(false);
        return CloudStoreResponse.RequireData(response, "workout injury impact");
    }

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
                record.IsActive && status != WorkoutInjuryStatus.Cleared));
        }

        return list;
    }

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
            date = loggedDate
        };

        var response = await apiClient.SendMutationAsync<object, InjuryObservationRecord>(
            HttpMethod.Put,
            InjuriesRoute,
            body,
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);
        var saved = CloudStoreResponse.RequireData(response, "save workout injury");
        CacheRecord(saved);
    }

    public async Task ClearInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        var existing = await RequireRecordAsync(injuryId, ct).ConfigureAwait(false);
        var body = new
        {
            id = existing.Id,
            siteFlag = existing.SiteFlag,
            status = WorkoutInjuryStatus.Cleared.ToString(),
            since = existing.Since,
            pain = existing.Pain,
            date = existing.Date
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

    public async Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default)
    {
        var profileResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, ct).ConfigureAwait(false);
        if (profileResponse.Status == BackendApiStatus.NotFound || profileResponse.Data is null)
            return;

        var profile = profileResponse.Data;
        var activities = WorkoutActivityPreferences.EnsureRequired(
                WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes))
            .Select(activity => (int)activity)
            .ToArray();
        var strengthStyle = (int)WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes);
        var daysPerWeek = profile.WorkoutDaysPerWeek > 0 ? profile.WorkoutDaysPerWeek : DefaultWorkoutDaysPerWeek;

        var response = await apiClient.SendMutationAsync<object, JsonElement>(
            HttpMethod.Put,
            WorkoutPreferencesRoute,
            new
            {
                expectedVersion = profile.Version,
                activityTypes = activities,
                strengthStyle,
                daysPerWeek
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            ct).ConfigureAwait(false);
        CloudStoreResponse.RequireSuccess(response, "refresh workout plan after injury change");
    }

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

    private async Task<InjuryObservationRecord> RequireRecordAsync(int injuryId, CancellationToken ct) =>
        await TryResolveRecordAsync(injuryId, ct).ConfigureAwait(false)
        ?? throw new KeyNotFoundException($"Injury {injuryId} was not found.");

    private int CacheRecord(InjuryObservationRecord record)
    {
        lock (_identityLock)
        {
            if (!_intIdByGuid.TryGetValue(record.Id, out var intId))
            {
                intId = ToStablePositiveIntId(record.Id);
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

    private static int ToStablePositiveIntId(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var raw = BitConverter.ToInt32(bytes[..4]) & int.MaxValue;
        return raw == 0 ? 1 : raw;
    }
}

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
            .Select(symptom => Enum.TryParse<SymptomType>(symptom.Type, true, out var parsed) && Enum.IsDefined(parsed)
                ? (SymptomType?)parsed
                : null)
            .Where(symptom => symptom.HasValue)
            .Select(symptom => symptom!.Value)
            .Distinct()
            .ToArray();

        var cycleStartedOnDate = overview.History.Cycles.Any(cycle => cycle.StartDate == dateOnly);
        var cycleEndedOnDate = overview.History.Cycles.Any(cycle => cycle.EndDate == dateOnly);

        if (phaseLog is null && daySymptoms.Length == 0 && !cycleStartedOnDate && !cycleEndedOnDate)
            return null;

        FlowIntensity? flow = null;
        if (!string.IsNullOrWhiteSpace(phaseLog?.FlowIntensityValue) &&
            Enum.TryParse<FlowIntensity>(phaseLog.FlowIntensityValue, true, out var parsedFlow) &&
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
            throw new InvalidOperationException("Record a period start before adding symptoms without a known cycle phase.");

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

    private static CyclePhase? ResolveTargetPhase(CycleDailyLogRequest request)
    {
        if (request.PhaseOverride.HasValue && request.PhaseOverride.Value != CyclePhase.Unknown)
            return request.PhaseOverride.Value;
        if (request.IsPeriodStart == true || (request.FlowIntensity.HasValue && request.FlowIntensity.Value != FlowIntensity.None))
            return CyclePhase.Menstrual;
        if (request.IsPeriodEnd == true)
            return CyclePhase.Follicular;
        return null;
    }

    internal static void ThrowPhaseOrderOrConflict(string? errorCode)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) &&
            Enum.TryParse<CyclePhase>(errorCode, true, out var suggestedPhase) &&
            Enum.IsDefined(suggestedPhase) && suggestedPhase != CyclePhase.Unknown)
        {
            throw new CyclePhaseOrderIssue("This phase would skip a step.", suggestedPhase);
        }

        throw new InvalidOperationException("Cycle phase update conflicted with existing cycle records.");
    }

    private static Guid DeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, GuidByteCount));
    }

    internal static void RequireSavedPhaseLog(BackendApiResponse<CloudPhaseLogSaveResponse> response)
    {
        var saved = CloudStoreResponse.RequireData(response, "save cycle phase");
        if (!string.Equals(saved.Outcome, "Saved", StringComparison.Ordinal))
            throw new InvalidOperationException("Cloud save cycle phase returned an invalid outcome.");
    }
}

internal sealed record CloudPhaseLogSaveResponse(string? Outcome);

public sealed class CloudCyclePhaseEditStore(
    IMuscleCutiesBackendApiClient apiClient) : ICyclePhaseEditStore
{
    private const string CyclePhasesRoute = "/api/v1/cycle/history/phases";

    public async Task<bool> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken)
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

        if (response.Status == BackendApiStatus.Conflict)
            CloudCycleDailyLogStore.ThrowPhaseOrderOrConflict(response.ErrorCode);

        CloudCycleDailyLogStore.RequireSavedPhaseLog(response);
        return true;
    }
}
