namespace MuscleCuties.Core.Services.Dashboard.Planning;

public sealed record DashboardSupportSummary(
    string CycleInsightText,
    string HydrationConsumed,
    string HydrationGoal,
    string SleepGoal,
    int ReadinessScore,
    string ReadinessLabel,
    int RecoveryScore,
    string RecoveryLabel);
