namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

/// <summary>
/// Typed reschedule failure. It derives from <see cref="InvalidOperationException"/> so the existing
/// route contract (400 with <c>{ error }</c>) carries its reason codes unchanged.
/// </summary>
public sealed class WorkoutRescheduleRejectedException : InvalidOperationException
{
    public const string Prefix = "WorkoutRescheduleRejected:";
    public const string SessionNotMovableCode = "WorkoutRescheduleSessionNotMovable";
    public const string SessionAlreadyStartedCode = "WorkoutRescheduleSessionAlreadyStarted";
    public const string ScheduleUnknownCode = "WorkoutRescheduleScheduleUnknown";

    /// <summary>Reports that the reschedule was refused, carrying the reason codes.</summary>
    public WorkoutRescheduleRejectedException(string reasonCode) : this([reasonCode])
    {
    }

    /// <summary>Carries the reasons a reschedule was refused, so the API can return them with the conflict.</summary>
    public WorkoutRescheduleRejectedException(IReadOnlyList<string> reasonCodes)
        : base(Prefix + string.Join(',', reasonCodes)) => ReasonCodes = reasonCodes;

    /// <summary>The reasons the reschedule was refused.</summary>
    public IReadOnlyList<string> ReasonCodes { get; }
}
