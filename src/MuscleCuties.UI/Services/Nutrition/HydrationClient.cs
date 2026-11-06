using System.Globalization;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Api;

namespace MuscleCuties.UI.Services.Nutrition;

internal sealed class HydrationRequestException(BackendApiStatus status, ApiFailureNotice notice)
    : Exception(notice.MessageText)
{
    public BackendApiStatus Status { get; } = status;
    public ApiFailureNotice Notice { get; } = notice;
}

public sealed class HydrationClient(IMuscleCutiesBackendApiClient api, IBackendSessionTokenStore tokenStore)
{
    private const string IntakeRoute = "/api/v1/hydration/intake";
    private const string PresetsRoute = "/api/v1/hydration/presets";
    private const string RemindersRoute = "/api/v1/hydration/reminders";

    internal async Task<Guid> RequireAccountAsync(CancellationToken ct)
    {
        var tokens = await tokenStore.ReadTokensAsync(ct).ConfigureAwait(false);
        return tokens is { AccountId: var id } && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    }

    internal async Task CheckAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (await RequireAccountAsync(ct).ConfigureAwait(false) != accountId)
            throw new UnauthorizedAccessException();
    }

    internal Task<HydrationDay> ReadDayAsync(DateOnly date, CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationDayAsync(api,
            IntakeRoute + "?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);

    internal Task<HydrationDay> ChangeDayAsync(HydrationDay day, Guid operationId, Guid entryId,
        HydrationIntakeChange change, int millilitres, CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationDayAsync(
            api, IntakeRoute, day.Date, day.Version, operationId, entryId, change, millilitres, ct);

    internal Task<HydrationPresetCatalog> ReadPresetsAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationPresetsAsync(api, PresetsRoute, ct);

    internal Task<HydrationPresetCatalog> InitializePresetsAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationPresetsAsync(
            api, HttpMethod.Post, PresetsRoute + "/initialize", new { }, null, ct);

    internal Task<HydrationPresetCatalog> SavePresetAsync(string? version, Guid? presetId, string label,
        int millilitres, Guid operationId, CancellationToken ct) =>
        NutritionPresentationMapper.ChangeHydrationPresetsAsync(api,
            presetId.HasValue ? HttpMethod.Put : HttpMethod.Post,
            presetId.HasValue ? PresetsRoute + $"/{presetId:D}" : PresetsRoute,
            new { Label = label, Millilitres = millilitres, OperationId = operationId, ExpectedVersion = version },
            operationId, ct);

    internal Task<HydrationPresetCatalog> DeletePresetAsync(string? version, Guid presetId, Guid operationId, CancellationToken ct)
    {
        var route = PresetsRoute + $"/{presetId:D}?operationId={operationId:D}";
        if (version is not null)
            route += "&expectedVersion=" + Uri.EscapeDataString(version);

        return NutritionPresentationMapper.ChangeHydrationPresetsAsync(api, HttpMethod.Delete,
            route, new { }, operationId, ct);
    }

    internal Task<HydrationPresetCatalog> ReorderPresetsAsync(string? version, IReadOnlyList<Guid> ids,
        Guid operationId, CancellationToken ct) => NutritionPresentationMapper.ChangeHydrationPresetsAsync(
            api, HttpMethod.Put, PresetsRoute + "/reorder",
            new { OrderedIds = ids, OperationId = operationId, ExpectedVersion = version }, operationId, ct);

    internal Task<HydrationReminder> ReadReminderAsync(CancellationToken ct) =>
        NutritionPresentationMapper.ReadHydrationReminderAsync(api, RemindersRoute, ct);

    internal Task<HydrationReminder> SaveReminderAsync(string? version, bool optedIn, IReadOnlyList<TimeOnly> times,
        TimeOnly? quietStart, TimeOnly? quietEnd, Guid operationId, CancellationToken ct) =>
        NutritionPresentationMapper.SaveHydrationReminderAsync(api, RemindersRoute,
            new { ExpectedVersion = version, OptedIn = optedIn, LocalTimes = times, QuietStart = quietStart,
                QuietEnd = quietEnd, OperationId = operationId }, operationId, ct);
}
