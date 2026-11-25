namespace MuscleCuties.Core.Services.Health;

/// <summary>Whether sync is on, what it last read and why it could not read.</summary>
public sealed record HealthSyncStatus(
    HealthDataSource? SelectedSource,
    bool IsConnected,
    bool PromptDismissed,
    DateTime? LastSyncedAt,
    string SummaryText);

/// <summary>The outcome of one read: the summary it built, or the failure it hit.</summary>
public sealed record HealthSyncResult(
    HealthDataSource Source,
    bool IsConnected,
    HealthWeeklySummary? Summary,
    string Message);
