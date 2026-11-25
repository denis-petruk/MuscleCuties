using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One recorded injury in the injury list, with its site, status, pain level and start date.</summary>
public sealed class InjuryLogItem
{
    /// <summary>Server identity of the injury.</summary>
    public int Id { get; init; }
    /// <summary>Affected site, used to match exercises for exclusion.</summary>
    public WorkoutInjurySite SiteFlag { get; init; }
    /// <summary>Localized site name for display.</summary>
    public string SiteName { get; init; } = string.Empty;
    /// <summary>Healing status that decides whether training is still restricted.</summary>
    public WorkoutInjuryStatus Status { get; init; }
    /// <summary>Localized status name for display.</summary>
    public string StatusName { get; init; } = string.Empty;
    /// <summary>Pain score on the 0 to 10 scale.</summary>
    public int Pain { get; init; }
    /// <summary>Pain score with its localized descriptor.</summary>
    public string PainLabel { get; init; } = string.Empty;
    /// <summary>Date the injury started, used for the reassessment reminder.</summary>
    public DateOnly Since { get; init; }
    /// <summary>Start date formatted for display.</summary>
    public string SinceText { get; init; } = string.Empty;
    /// <summary>True while the injury still restricts the plan.</summary>
    public bool IsActive { get; init; }
    /// <summary>True when enough time has passed to ask for a fresh pain score.</summary>
    public bool IsPainReassessmentDue { get; init; }

    /// <summary>Closed-set answer to the worsening-movement question, as stored.</summary>
    public MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger { get; init; }

    /// <summary>True when a clinician advised avoiding the movement; null means not answered.</summary>
    public bool? ClinicianAdvisedAvoid { get; init; }
}
