namespace MuscleCuties.Core.Services.Health;

public sealed record HealthSyncStatus(
    HealthDataSource? SelectedSource,
    bool IsConnected,
    bool PromptDismissed,
    DateTime? LastSyncedAt,
    string SummaryText);

public sealed record HealthSyncResult(
    HealthDataSource Source,
    bool IsConnected,
    HealthWeeklySummary? Summary,
    string Message);
