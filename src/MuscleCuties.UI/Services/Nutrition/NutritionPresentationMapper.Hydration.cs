using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UI.Services.Nutrition;

public static partial class NutritionPresentationMapper
{
    internal static int MaximumWaterPresetLabelLength => WaterPreset.MaximumLabelLength;
    internal static int MaximumWaterPresetCount => WaterPresetCatalog.MaximumPresetCount;
    internal static int MaximumWaterReminderTimes => FixedWaterReminderSettings.MaximumReminderTimes;

    internal static bool IsValidWaterVolume(int millilitres) =>
        millilitres is >= WaterPortion.MinimumMillilitres and <= WaterPortion.MaximumMillilitres;

    internal static bool IsValidWaterReminder(IReadOnlyList<TimeOnly> times, TimeOnly quietStart, TimeOnly quietEnd)
    {
        try
        {
            FixedWaterReminderSettings.Enable(times, quietStart, quietEnd);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static async Task<HydrationDay> ReadHydrationDayAsync(IMuscleCutiesBackendApiClient api,
        string route, CancellationToken ct) => FromHydrationDay(
            RequireHydrationData(await api.GetAsync<WaterIntakeDayOutputDto>(route, ct).ConfigureAwait(false)));

    internal static async Task<HydrationDay> ChangeHydrationDayAsync(IMuscleCutiesBackendApiClient api,
        string route, DateOnly date, string? version, Guid operationId, Guid entryId,
        HydrationIntakeChange change, int millilitres, CancellationToken ct)
    {
        var kind = change switch
        {
            HydrationIntakeChange.Add => WaterIntakeChangeKindDto.Add,
            HydrationIntakeChange.Correct => WaterIntakeChangeKindDto.Correct,
            HydrationIntakeChange.Undo => WaterIntakeChangeKindDto.Undo,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        return FromHydrationDay(RequireHydrationData(await api.SendMutationAsync<object, WaterIntakeDayOutputDto>(
            HttpMethod.Post, route,
            new { Date = date, ExpectedVersion = version, OperationId = operationId, EntryId = entryId, Kind = kind, Millilitres = millilitres },
            operationId.ToString("D"), ct).ConfigureAwait(false)));
    }

    internal static async Task<HydrationPresetCatalog> ReadHydrationPresetsAsync(IMuscleCutiesBackendApiClient api,
        string route, CancellationToken ct) => FromHydrationPresets(
            RequireHydrationData(await api.GetAsync<WaterPresetCatalogOutputDto>(route, ct).ConfigureAwait(false)));

    internal static async Task<HydrationPresetCatalog> ChangeHydrationPresetsAsync(IMuscleCutiesBackendApiClient api,
        HttpMethod method, string route, object body, Guid? operationId, CancellationToken ct) => FromHydrationPresets(
            RequireHydrationData(await api.SendMutationAsync<object, WaterPresetCatalogOutputDto>(
                method, route, body, operationId?.ToString("D"), ct).ConfigureAwait(false)));

    internal static async Task<HydrationReminder> ReadHydrationReminderAsync(IMuscleCutiesBackendApiClient api,
        string route, CancellationToken ct) => FromHydrationReminder(
            RequireHydrationData(await api.GetAsync<WaterReminderConfigurationOutputDto>(route, ct).ConfigureAwait(false)));

    internal static async Task<HydrationReminder> SaveHydrationReminderAsync(IMuscleCutiesBackendApiClient api,
        string route, object body, Guid operationId, CancellationToken ct) => FromHydrationReminder(
            RequireHydrationData(await api.SendMutationAsync<object, WaterReminderConfigurationOutputDto>(
                HttpMethod.Put, route, body, operationId.ToString("D"), ct).ConfigureAwait(false)));

    private static HydrationDay FromHydrationDay(WaterIntakeDayOutputDto day) => new(
        day.AccountId, day.Date, day.TotalMillilitres, day.Entries
            .Select((entry, index) => (Entry: entry, Number: index + 1))
            .Where(item => !item.Entry.Undone)
            .Select(item => new HydrationEntry(item.Entry.Id, item.Entry.Millilitres, item.Number))
            .Reverse().ToArray(), day.Version);

    private static HydrationPresetCatalog FromHydrationPresets(WaterPresetCatalogOutputDto catalog) => new(
        catalog.AccountId, catalog.Version, catalog.Presets
            .Select(preset => new HydrationPreset(preset.Id, preset.Label, preset.Millilitres)).ToArray());

    private static HydrationReminder FromHydrationReminder(WaterReminderConfigurationOutputDto reminder) => new(
        reminder.AccountId, reminder.Version, reminder.OptedIn, reminder.LocalTimes, reminder.QuietStart, reminder.QuietEnd);

    private static T RequireHydrationData<T>(BackendApiResponse<T> response) where T : class =>
        response.Status == BackendApiStatus.Success && response.Data is { } data
            ? data
            : throw new HydrationRequestException(response.Status,
                ApiFailurePresenter.FromResponse(response,
                    response.ErrorCode == "hydrationLegacyTotalConflict"
                        ? "Nutrition_Hydration_LegacyTotalConflict"
                        : "Nutrition_Hydration_RequestFailed"));
}
