using System.Text.Json;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.Services.Health;

public sealed class HealthSyncService : IHealthSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<IHealthDataProvider> _providers;
    private readonly ITokenStorage _tokenStorage;

    public HealthSyncService(IEnumerable<IHealthDataProvider> providers, ITokenStorage tokenStorage)
    {
        _providers = providers.ToList();
        _tokenStorage = tokenStorage;
    }

    public async Task<HealthSyncStatus> GetStatusAsync(int userId)
    {
        var state = await ReadStateAsync(userId);
        var summary = await GetCachedWeeklySummaryAsync(userId);

        return new HealthSyncStatus(
            state.SelectedSource,
            state.IsConnected,
            state.PromptDismissed,
            state.LastSyncedAt,
            BuildSummaryText(state, summary));
    }

    public async Task<bool> ShouldShowPromptAsync(int userId)
    {
        var status = await GetStatusAsync(userId);
        return !status.IsConnected && !status.PromptDismissed;
    }

    public async Task DismissPromptAsync(int userId)
    {
        var state = await ReadStateAsync(userId);
        await WriteStateAsync(userId, state with { PromptDismissed = true });
    }

    public async Task<HealthSyncResult> SyncAsync(
        int userId,
        HealthDataSource source,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(item => item.Source == source);
        if (provider is null)
            return new HealthSyncResult(source, false, null, $"{source.ToDisplayName()} is not available on this device yet.");

        if (!await provider.IsAvailableAsync(cancellationToken))
        {
            await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
            return new HealthSyncResult(source, false, null, BuildUnavailableMessage(provider));
        }

        var summary = await provider.ReadWeeklySummaryAsync(DateTime.Today, cancellationToken);
        if (summary is null)
        {
            await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
            return new HealthSyncResult(source, false, null, BuildEmptyDataMessage(provider));
        }

        await _tokenStorage.SetAsync(SummaryKey(userId), JsonSerializer.Serialize(summary, JsonOptions));
        await WriteStateAsync(userId, new StoredHealthSyncState(source, true, true, summary.SyncedAt));
        return new HealthSyncResult(source, true, summary, $"{provider.DisplayName} is connected.");
    }

    public async Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId)
    {
        var json = await _tokenStorage.GetAsync(SummaryKey(userId));
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<HealthWeeklySummary>(json, JsonOptions);
        }
        catch (JsonException)
        {
            _tokenStorage.Remove(SummaryKey(userId));
            return null;
        }
    }

    private async Task<StoredHealthSyncState> ReadStateAsync(int userId)
    {
        var json = await _tokenStorage.GetAsync(StateKey(userId));
        if (string.IsNullOrWhiteSpace(json))
            return StoredHealthSyncState.Empty;

        try
        {
            var state = JsonSerializer.Deserialize<StoredHealthSyncState>(json, JsonOptions)
                        ?? StoredHealthSyncState.Empty;
            return IsSupportedSource(state.SelectedSource) ? state : StoredHealthSyncState.Empty;
        }
        catch (JsonException)
        {
            _tokenStorage.Remove(StateKey(userId));
            return StoredHealthSyncState.Empty;
        }
    }

    private Task WriteStateAsync(int userId, StoredHealthSyncState state) =>
        _tokenStorage.SetAsync(StateKey(userId), JsonSerializer.Serialize(state, JsonOptions));

    private static string BuildSummaryText(StoredHealthSyncState state, HealthWeeklySummary? summary)
    {
        if (!state.IsConnected || state.SelectedSource is null)
            return "Not connected";

        if (summary is null)
            return $"{state.SelectedSource.Value.ToDisplayName()} connected";

        return $"{state.SelectedSource.Value.ToDisplayName()} · {summary.MovementSummary} · {summary.SleepSummary}";
    }

    private static string BuildUnavailableMessage(IHealthDataProvider provider) =>
        provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.UnavailableMessage
            : $"{provider.DisplayName} is not available on this device yet.";

    private static string BuildEmptyDataMessage(IHealthDataProvider provider) =>
        provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.EmptyDataMessage
            : $"{provider.DisplayName} did not return step or sleep data yet.";

    private static bool IsSupportedSource(HealthDataSource? source) =>
        source is null || Enum.IsDefined(source.Value);

    private static string StateKey(int userId) => $"health_sync_state_{userId}";
    private static string SummaryKey(int userId) => $"health_sync_weekly_summary_{userId}";

    private sealed record StoredHealthSyncState(
        HealthDataSource? SelectedSource,
        bool IsConnected,
        bool PromptDismissed,
        DateTime? LastSyncedAt)
    {
        public static StoredHealthSyncState Empty { get; } = new(null, false, false, null);
    }
}
