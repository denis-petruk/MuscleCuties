using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

public sealed class InjuryLogItem
{
    public int Id { get; init; }
    public WorkoutInjurySite SiteFlag { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public WorkoutInjuryStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int Pain { get; init; }
    public string PainLabel { get; init; } = string.Empty;
    public DateOnly Since { get; init; }
    public string SinceText { get; init; } = string.Empty;
    public bool IsActive { get; init; }

    /// <summary>Closed-set answer to the worsening-movement question, as stored.</summary>
    public MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger { get; init; }

    /// <summary>True when a clinician advised avoiding the movement; null means not answered.</summary>
    public bool? ClinicianAdvisedAvoid { get; init; }
}
