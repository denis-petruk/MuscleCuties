namespace MuscleCuties.Core.Models.UI.Workout;

public sealed class InjuryLogItem
{
    public int Id { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public int Pain { get; init; }
    public string SinceText { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
