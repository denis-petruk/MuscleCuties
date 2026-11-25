using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.Core.Services.Health;

/// <summary>The health sync: it talks to the device's health provider, keeps the last summary per account, and limits</summary>
/// itself to steps, sleep, active energy, resting heart rate and heart-rate variability.</summary>
public sealed class HealthSyncService : IHealthSyncService, IDisposable
{
    // 2: seven completed-day step baseline and merged sleep intervals (2026-11-10). Version-1 summaries are discarded.
    private const int StepCountStateVersion = 2;
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(45);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<IHealthDataProvider> _providers;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly ITokenStorage _tokenStorage;

    /// <summary>Takes the available providers so the service can work on a platform where health data is not available at</summary>
    /// all.</summary>
    public HealthSyncService(IEnumerable<IHealthDataProvider> providers, ITokenStorage tokenStorage)
    {
        _providers = providers.ToList();
        _tokenStorage = tokenStorage;
    }

    /// <summary>The provider that actually reads this device's data, or nothing when none can.</summary>
    private IHealthDataProvider? DeviceHealthProvider =>
        _providers.FirstOrDefault(provider =>
            provider.Source is HealthDataSource.AppleHealth or HealthDataSource.HealthConnect);

    /// <summary>Stops listening for health store changes.</summary>
    public void Dispose()
    {
        _syncGate.Dispose();
    }

    /// <summary>Which health store the data would come from, for the label the settings page shows.</summary>
    public HealthDataSource DeviceHealthSource =>
        DeviceHealthProvider?.Source ?? HealthDataSource.AppleHealth;

    /// <summary>The store's name as the user knows it, with a neutral fallback when there is none.</summary>
    public string DeviceHealthDisplayName =>
        DeviceHealthProvider?.DisplayName ?? DeviceHealthSource.ToDisplayName();

    /// <summary>True when the device exposes a health provider.</summary>
    public bool IsAvailable => DeviceHealthProvider is not null;

    /// <summary>Whether sync is on for this account, and what it last read.</summary>
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

    /// <summary>Whether the connect prompt is still worth showing, which it is not once the user has dismissed it.</summary>
    public async Task<bool> ShouldShowPromptAsync(int userId)
    {
        var state = await ReadStateAsync(userId);
        if (state.IsConnected || state.PromptDismissed)
            return false;

        return await HasAvailableProviderAsync();
    }

    /// <summary>Remembers that the prompt was declined, so it is not shown again.</summary>
    public async Task DismissPromptAsync(int userId)
    {
        var state = await ReadStateAsync(userId);
        await WriteStateAsync(userId, state with { PromptDismissed = true });
    }

    /// <summary>Turns sync off for the account and forgets the stored state, so no health data outlives the choice.</summary>
    public async Task DisconnectAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return;

        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            // These keys include the current account id; other accounts remain untouched.
            _tokenStorage.Remove(SummaryKey(userId));
            _tokenStorage.Remove(StateKey(userId));
        }
        finally
        {
            _syncGate.Release();
        }
    }

    /// <summary>Reads steps, sleep, energy, resting heart rate and HRV, then stores the result.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
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
                _tokenStorage.Remove(SummaryKey(userId));
                await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
                return new HealthSyncResult(source, false, null, BuildUnavailableMessage(provider));
            }

            if (provider is IHealthAuthorizationProvider authorization &&
                !await authorization.RequestReadAuthorizationAsync(syncToken))
            {
                _tokenStorage.Remove(SummaryKey(userId));
                await WriteStateAsync(userId, new StoredHealthSyncState(source, false, false, null));
                return new HealthSyncResult(source, false, null,
                    $"{provider.DisplayName} could not open the permission request. Try again in Settings.");
            }

            var summary = await provider.ReadWeeklySummaryAsync(DateTime.Today, syncToken);
            if (summary is null)
            {
                _tokenStorage.Remove(SummaryKey(userId));
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
        catch (Exception ex)
        {
            Trace.WriteLine($"[{nameof(HealthSyncService)}] Provider sync failed ({ex.GetType().Name}).");
            return new HealthSyncResult(source, false, null,
                $"{provider.DisplayName} could not sync right now. Check permissions and try again.");
        }
        finally
        {
            if (gateHeld)
                _syncGate.Release();
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    /// <summary>The last weekly summary for the account, or nothing when none has been read yet.</summary>
    public async Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId)
    {
        var state = await ReadStateAsync(userId);
        if (!state.IsConnected)
            return null;

        var json = await _tokenStorage.GetAsync(SummaryKey(userId));
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var summary = JsonSerializer.Deserialize<HealthWeeklySummary>(json, JsonOptions);
            return summary is not null && summary.SyncedAt >= DateTime.UtcNow.AddHours(-36)
                ? summary
                : null;
        }
        catch (JsonException)
        {
            _tokenStorage.Remove(SummaryKey(userId));
            return null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    /// <summary>The stored sync state for the account, in the encrypted local store.</summary>
    private async Task<StoredHealthSyncState> ReadStateAsync(int userId)
    {
        var json = await _tokenStorage.GetAsync(StateKey(userId));
        if (string.IsNullOrWhiteSpace(json))
            return StoredHealthSyncState.Empty;

        try
        {
            var state = JsonSerializer.Deserialize<StoredHealthSyncState>(json, JsonOptions)
                        ?? StoredHealthSyncState.Empty;
            if (state.SchemaVersion == StepCountStateVersion && IsSupportedSource(state.SelectedSource))
                return state;

            // Discard cached summaries created by the former multi-metric HealthKit reader.
            _tokenStorage.Remove(SummaryKey(userId));
            _tokenStorage.Remove(StateKey(userId));
            return StoredHealthSyncState.Empty;
        }
        catch (JsonException)
        {
            _tokenStorage.Remove(StateKey(userId));
            return StoredHealthSyncState.Empty;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    /// <summary>Stores the sync state, encrypted, under the account's own key.</summary>
    private Task WriteStateAsync(int userId, StoredHealthSyncState state)
    {
        return _tokenStorage.SetAsync(StateKey(userId),
            JsonSerializer.Serialize(state with { SchemaVersion = StepCountStateVersion }, JsonOptions));
    }

    /// <summary>The one-line summary of what was read, or the reason there is nothing to show.</summary>
    private static string BuildSummaryText(StoredHealthSyncState state, HealthWeeklySummary? summary)
    {
        if (!state.IsConnected || state.SelectedSource is null)
            return "Not connected";

        if (summary is null)
            return $"{state.SelectedSource.Value.ToDisplayName()} connected";

        var parts = new List<string>
        {
            state.SelectedSource.Value.ToDisplayName(),
            summary.MovementSummary
        };
        parts.Add(summary.SleepSummary);
        if (summary.RestingHeartRate > 0)
            parts.Add($"{summary.RestingHeartRate} bpm RHR");
        if (summary.AverageActiveEnergyKcal is > 0)
            parts.Add($"{summary.AverageActiveEnergyKcal.Value:N0} kcal active");

        return string.Join(" · ", parts);
    }

    /// <summary>Why this device cannot share health data, in the user's words.</summary>
    private static string BuildUnavailableMessage(IHealthDataProvider provider)
    {
        return provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.UnavailableMessage
            : $"{provider.DisplayName} is not available on this device yet.";
    }

    /// <summary>What to say when the provider works but the period holds no data.</summary>
    private static string BuildEmptyDataMessage(IHealthDataProvider provider)
    {
        return provider is IHealthDataProviderDiagnostics diagnostics
            ? diagnostics.EmptyDataMessage
            : $"{provider.DisplayName} did not return step or sleep data yet.";
    }

    /// <summary>Whether any provider can actually read data right now.</summary>
    private async Task<bool> HasAvailableProviderAsync()
    {
        foreach (var provider in _providers)
            try
            {
                if (await provider.IsAvailableAsync())
                    return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"[{nameof(HealthSyncService)}] Provider availability check failed ({ex.GetType().Name}).");
            }

        return false;
    }

    /// <summary>Whether a stored source is one this build still supports, so an old state is not read as current.</summary>
    private static bool IsSupportedSource(HealthDataSource? source)
    {
        return source is null || Enum.IsDefined(source.Value);
    }

    /// <summary>The storage key for one account's sync state, which is what keeps accounts from reading each other's.</summary>
    private static string StateKey(int userId)
    {
        return $"health_sync_state_{userId}";
    }

    /// <summary>The storage key for one account's cached summary.</summary>
    private static string SummaryKey(int userId)
    {
        return $"health_sync_weekly_summary_{userId}";
    }

    /// <summary>What the service remembers per account: whether sync is on, which source it used and the last summary it</summary>
    /// built.</summary>
    private sealed record StoredHealthSyncState(
        HealthDataSource? SelectedSource,
        bool IsConnected,
        bool PromptDismissed,
        DateTime? LastSyncedAt)
    {
        /// <summary>The shape version of the stored state, so an older record is recognised rather than misread.</summary>
        public int SchemaVersion { get; init; }
        /// <summary>The state a device starts from before any sync.</summary>
        public static StoredHealthSyncState Empty { get; } = new(null, false, false, null);
    }
}
