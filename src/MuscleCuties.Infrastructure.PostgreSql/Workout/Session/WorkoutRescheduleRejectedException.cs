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

    public WorkoutRescheduleRejectedException(string reasonCode) : this([reasonCode])
    {
    }

    public WorkoutRescheduleRejectedException(IReadOnlyList<string> reasonCodes)
        : base(Prefix + string.Join(',', reasonCodes)) => ReasonCodes = reasonCodes;

    public IReadOnlyList<string> ReasonCodes { get; }
}
