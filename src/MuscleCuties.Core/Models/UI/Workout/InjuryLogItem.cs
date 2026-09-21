using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public sealed class InjuryLogItem
{
    public int Id { get; init; }
    public InjuryFlag SiteFlag { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public InjuryStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int Pain { get; init; }
    public string PainLabel { get; init; } = string.Empty;
    public DateOnly Since { get; init; }
    public string SinceText { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
