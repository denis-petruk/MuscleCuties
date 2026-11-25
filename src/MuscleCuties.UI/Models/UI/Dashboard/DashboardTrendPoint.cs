namespace MuscleCuties.UI.Models.UI.Dashboard;

/// <summary>One day on the dashboard trend chart; either series may be absent for that day,</summary>
/// which leaves a gap in that line instead of a zero.</summary>
public sealed record DashboardTrendPoint(DateOnly Date, double? WeightKg, int? ReadinessScore);
