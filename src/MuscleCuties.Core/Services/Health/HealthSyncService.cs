using System.Text.Json;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.Services.Health;

public sealed class HealthSyncService : IHealthSyncService
{
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(45);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<IHealthDataProvider> _providers;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly ITokenStorage _tokenStorage;

    public HealthSyncService(IEnumerable<IHealthDataProvider> providers, ITokenStorage tokenStorage)
    {
        _providers = providers.ToList();
        _tokenStorage = tokenStorage;
    }

    private IHealthDataProvider? DeviceHealthProvider =>
        _providers.FirstOrDefault(provider =>
            provider.Source is HealthDataSource.AppleHealth or HealthDataSource.HealthConnect);

    public HealthDataSource DeviceHealthSource =>
        DeviceHealthProvider?.Source ?? HealthDataSource.AppleHealth;

    public string DeviceHealthDisplayName =>
        DeviceHealthProvider?.DisplayName ?? DeviceHealthSource.ToDisplayName();

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
        var state = await ReadStateAsync(userId);
        if (state.IsConnected || state.PromptDismissed)
            return false;

        return await HasAvailableProviderAsync();
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
            return new HealthSyncResult(source, false, null,
                $"{source.ToDisplayName()} is not available on this device yet.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SyncTimeout);
        var syncToken = timeoutCts.Token;
        var gateHeld = false;

        try
        {
            await _syncGate.WaitAsync(syncToken);
            gateHeld = true;

            if (!await provider.IsAvailableAsync(syncToken))
            {
                await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
                return new HealthSyncResult(source, false, null, BuildUnavailableMessage(provider));
            }

            var summary = await provider.ReadWeeklySummaryAsync(DateTime.Today, syncToken);
            if (summary is null)
            {
                await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
                return new HealthSyncResult(source, false, null, BuildEmptyDataMessage(provider));
            }

            await _tokenStorage.SetAsync(SummaryKey(userId), JsonSerializer.Serialize(summary, JsonOptions));
            await WriteStateAsync(userId, new StoredHealthSyncState(source, true, true, summary.SyncedAt));
            return new HealthSyncResult(source, true, summary, $"{provider.DisplayName} is connected.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new HealthSyncResult(source, false, null,
                $"{provider.DisplayName} took too long to respond. Try again when it is ready.");
        }
        catch (Exception)
        {
            return new HealthSyncResult(source, false, null,
                $"{provider.DisplayName} could not sync right now. Check permissions and try again.");
        }
        finally
        {
            if (gateHeld)
                _syncGate.Release();
        }
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

    private Task WriteStateAsync(int userId, StoredHealthSyncState state)
    {
        return _tokenStorage.SetAsync(StateKey(userId), JsonSerializer.Serialize(state, JsonOptions));
    }

    private static string BuildSummaryText(StoredHealthSyncState state, HealthWeeklySummary? summary)
    {
        if (!state.IsConnected || state.SelectedSource is null)
            return "Not connected";

        if (summary is null)
            return $"{state.SelectedSource.Value.ToDisplayName()} connected";

        return $"{state.SelectedSource.Value.ToDisplayName()} · {summary.MovementSummary} · {summary.SleepSummary}";
    }

    private static string BuildUnavailableMessage(IHealthDataProvider provider)
    {
        return provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.UnavailableMessage
            : $"{provider.DisplayName} is not available on this device yet.";
    }

    private static string BuildEmptyDataMessage(IHealthDataProvider provider)
    {
        return provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.EmptyDataMessage
            : $"{provider.DisplayName} did not return step or sleep data yet.";
    }

    private async Task<bool> HasAvailableProviderAsync()
    {
        foreach (var provider in _providers)
            try
            {
                if (await provider.IsAvailableAsync())
                    return true;
            }
            catch
            {
            }

        return false;
    }

    private static bool IsSupportedSource(HealthDataSource? source)
    {
        return source is null || Enum.IsDefined(source.Value);
    }

    private static string StateKey(int userId)
    {
        return $"health_sync_state_{userId}";
    }

    private static string SummaryKey(int userId)
    {
        return $"health_sync_weekly_summary_{userId}";
    }

    private sealed record StoredHealthSyncState(
        HealthDataSource? SelectedSource,
        bool IsConnected,
        bool PromptDismissed,
        DateTime? LastSyncedAt)
    {
        public static StoredHealthSyncState Empty { get; } = new(null, false, false, null);
    }
}
