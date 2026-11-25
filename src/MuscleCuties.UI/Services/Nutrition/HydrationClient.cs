using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Api;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>A hydration request the server answered with a failure, carrying the status and the message the UI</summary>
/// should show.</summary>
internal sealed class HydrationRequestException(BackendApiStatus status, ApiFailureNotice notice)
    : Exception(notice.MessageText)
{
    /// <summary>The status the call ended with.</summary>
    public BackendApiStatus Status { get; } = status;
    /// <summary>The failure notice, when the call failed.</summary>
    public ApiFailureNotice Notice { get; } = notice;
}

/// <summary>The hydration endpoints: the day's intake, presets and reminders, each scoped to the signed-in</summary>
/// account.</summary>
public sealed class HydrationClient(IMuscleCutiesBackendApiClient api, IBackendSessionTokenStore tokenStore)
{
    private const string IntakeRoute = "/api/v1/hydration/intake";
    private const string PresetsRoute = "/api/v1/hydration/presets";
    private const string RemindersRoute = "/api/v1/hydration/reminders";

    /// <summary>The signed-in account's id, or an unauthorized exception when there is no session.</summary>
    internal async Task<Guid> RequireAccountAsync(CancellationToken ct)
    {
        var tokens = await tokenStore.ReadTokensAsync(ct).ConfigureAwait(false);
        return tokens is { AccountId: var id } && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    }

    /// <summary>Rejects a request aimed at any account other than the signed-in one, so a stale screen cannot read</summary>
    /// or write another user's hydration.</summary>
    internal async Task CheckAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (await RequireAccountAsync(ct).ConfigureAwait(false) != accountId)
            throw new UnauthorizedAccessException();
    }

    /// <summary>Reads the hydration day.</summary>
    internal Task<HydrationDay> ReadDayAsync(DateOnly date, CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationDayAsync(api,
            IntakeRoute + "?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);

    /// <summary>Applies one intake change to the day.</summary>
    internal Task<HydrationDay> ChangeDayAsync(HydrationDay day, Guid operationId, Guid entryId,
        HydrationIntakeChange change, int millilitres, CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationDayAsync(
            api, IntakeRoute, day.Date, day.Version, operationId, entryId, change, millilitres, ct);

    /// <summary>Reads the water presets.</summary>
    internal Task<HydrationPresetCatalog> ReadPresetsAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationPresetsAsync(api, PresetsRoute, ct);

    /// <summary>Creates the default presets.</summary>
    internal Task<HydrationPresetCatalog> InitializePresetsAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationPresetsAsync(
            api, HttpMethod.Post, PresetsRoute + "/initialize", new { }, null, ct);

    /// <summary>Saves one water preset.</summary>
    internal Task<HydrationPresetCatalog> SavePresetAsync(string? version, Guid? presetId, string label,
        int millilitres, Guid operationId, CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationPresetsAsync(api,
            presetId.HasValue ? HttpMethod.Put : HttpMethod.Post,
            presetId.HasValue ? PresetsRoute + $"/{presetId:D}" : PresetsRoute,
            new { Label = label, Millilitres = millilitres, OperationId = operationId, ExpectedVersion = version },
            operationId, ct);

    /// <summary>Deletes a preset, sending the version when known so a concurrent edit is rejected instead of</summary>
    /// overwritten, and the operation id so a retry is not applied twice.</summary>
    internal Task<HydrationPresetCatalog> DeletePresetAsync(string? version, Guid presetId, Guid operationId, CancellationToken ct)
    {
        var route = PresetsRoute + $"/{presetId:D}?operationId={operationId:D}";
        if (version is not null)
            route += "&expectedVersion=" + Uri.EscapeDataString(version);

        return NutritionPresentationMapper.ChangeHydrationPresetsAsync(api, HttpMethod.Delete,
            route, new { }, operationId, ct);
    }

    /// <summary>Stores the new preset order.</summary>
    internal Task<HydrationPresetCatalog> ReorderPresetsAsync(string? version, IReadOnlyList<Guid> ids,
        Guid operationId, CancellationToken ct) => NutritionPresentationMapper.ChangeHydrationPresetsAsync(
            api, HttpMethod.Put, PresetsRoute + "/reorder",
            new { OrderedIds = ids, OperationId = operationId, ExpectedVersion = version }, operationId, ct);

    /// <summary>Reads the reminder settings.</summary>
    internal Task<HydrationReminder> ReadReminderAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationReminderAsync(api, RemindersRoute, ct);

    /// <summary>Saves the reminder settings.</summary>
    internal Task<HydrationReminder> SaveReminderAsync(string? version, bool optedIn, IReadOnlyList<TimeOnly> times,
        TimeOnly? quietStart, TimeOnly? quietEnd, Guid operationId, CancellationToken ct) =>
        NutritionPresentationMapper.SaveHydrationReminderAsync(api, RemindersRoute,
            new { ExpectedVersion = version, OptedIn = optedIn, LocalTimes = times, QuietStart = quietStart,
                QuietEnd = quietEnd, OperationId = operationId }, operationId, ct);
}
