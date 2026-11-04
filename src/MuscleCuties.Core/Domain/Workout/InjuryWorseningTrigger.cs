namespace MuscleCuties.Core.Domain.Workout;

/// <summary>
/// Answer to "which movements make this injury worse?". A closed set keeps substitution
/// deterministic and keeps free-text health descriptions out of storage and logs.
/// <c>NotAnswered</c> is deliberately distinct from <c>None</c>: an unanswered question is not
/// evidence that the injury is settling.
/// </summary>
public enum InjuryWorseningTrigger
{
    NotAnswered = 0,
    None = 1,
    OverheadReach = 2,
    DeepKneeFlexion = 3,
    SpinalLoading = 4,
    ImpactAndGait = 5,
    GripAndWristLoad = 6
}
