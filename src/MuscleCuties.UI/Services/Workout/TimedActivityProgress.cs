namespace MuscleCuties.UI.Services.Workout;

/// <summary>
/// Derives the visible timed-activity phase and countdown from confirmed active time and the
/// activity's own prescription. Duration-only activities stay usable: when no phase data exists the
/// helper reports elapsed time only instead of inventing a prescription.
/// </summary>
public static class TimedActivityProgress
{
    public const string WarmupPhaseKey = "Workout_Session_TimedPhaseWarmup";
    public const string MainPhaseKey = "Workout_Session_TimedPhaseMain";
    public const string CooldownPhaseKey = "Workout_Session_TimedPhaseCooldown";

    /// <summary>Inclusive effort bounds shared with the session finish validation.</summary>
    public const int MinimumRpe = 1;
    public const int MaximumRpe = 10;

    private const string RestTag = "REST";

    /// <summary>
    /// Converts the optional effort control into the value the finish route accepts. An unrated
    /// activity stays null instead of inventing an effort value; out-of-range input is discarded.
    /// </summary>
    public static int? NormalizeRpe(double? value)
    {
        if (value is not { } rating || double.IsNaN(rating) || double.IsInfinity(rating)) return null;
        var rounded = (int)Math.Round(rating, MidpointRounding.AwayFromZero);
        return rounded is >= MinimumRpe and <= MaximumRpe ? rounded : null;
    }

    /// <summary>Upper bound shared by the distance input and the finish validation.</summary>
    public const decimal MaximumDistanceKm = 500m;

    /// <summary>
    /// Distance is an optional actual only for cardio-style timed activities. Recovery, mobility and
    /// rest never ask for it, so the field cannot imply a metric the activity does not have.
    /// </summary>
    public static bool SupportsDistance(string? tag) =>
        !string.IsNullOrWhiteSpace(tag) && tag.Equals("CARDIO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Keeps an entered distance within the supported bound. Blank or zero means "not recorded"
    /// rather than zero kilometres, and out-of-range input is discarded instead of clamped.
    /// </summary>
    public static decimal? NormalizeDistance(decimal? value) =>
        value is > 0m and <= MaximumDistanceKm
            ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>
    /// The surface a workout-day section runs on. One rule owns this decision so the view model, the
    /// section model and the contract checks can never disagree.
    /// </summary>
    public enum WorkoutActivitySurface
    {
        /// <summary>Shared timed lifecycle: phase, elapsed, remaining, pause/resume, finish summary.</summary>
        TimedActivity,

        /// <summary>Exercise-by-exercise flow: strength sets or a mixed day's movement log.</summary>
        ExerciseFlow,

        /// <summary>Rest day: no start action at all.</summary>
        RestDay
    }

    /// <summary>
    /// Surface rule. Rest days never start. A section without planned movement rows is always a timed
    /// activity: the planner assigns movement rows only to strength activities, so a row-less section —
    /// cardio, recovery, yoga, mobility, gentle recovery, or climbing, which the planner persists as a
    /// strength-type day without rows — has nothing to log exercise by exercise. A section that does
    /// carry movement rows keeps the existing exercise flow, because those rows collect distance, pace,
    /// heart-rate and power actuals that the planned-session finish route does not persist; sending them
    /// to the timed surface would silently drop recorded metrics, and adding a second logging path would
    /// fork the single timing authority. Unknown or empty tags follow the same structural rule, so an
    /// unroutable section is never stranded on an empty exercise flow.
    /// </summary>
    public static WorkoutActivitySurface DecideSurface(string? tag, bool hasPlannedMovements)
    {
        if (IsRestTag(tag)) return WorkoutActivitySurface.RestDay;
        return hasPlannedMovements
            ? WorkoutActivitySurface.ExerciseFlow
            : WorkoutActivitySurface.TimedActivity;
    }

    /// <summary>Rest days are the only tag this rule still reads directly.</summary>
    public static bool IsRestTag(string? tag) =>
        !string.IsNullOrWhiteSpace(tag) && tag.Equals(RestTag, StringComparison.OrdinalIgnoreCase);

    public sealed record State(
        string PhaseKey, int ElapsedSeconds, int? RemainingSeconds, int? TotalSeconds, double Progress)
    {
        public bool HasPhase => !string.IsNullOrEmpty(PhaseKey);
        public bool HasCountdown => RemainingSeconds.HasValue;
    }

    public static State Describe(
        int elapsedSeconds, decimal? warmupMinutes, decimal? mainMinutes, decimal? cooldownMinutes)
    {
        var elapsed = Math.Max(0, elapsedSeconds);
        var warmup = ToSeconds(warmupMinutes);
        var main = ToSeconds(mainMinutes);
        var cooldown = ToSeconds(cooldownMinutes);

        if (warmup is null && main is null && cooldown is null)
            return new State(string.Empty, elapsed, null, null, 0);

        var warmupSeconds = warmup ?? 0;
        var mainSeconds = main ?? 0;
        var cooldownSeconds = cooldown ?? 0;
        var total = warmupSeconds + mainSeconds + cooldownSeconds;

        var phase = elapsed < warmupSeconds ? WarmupPhaseKey
            : elapsed < warmupSeconds + mainSeconds ? MainPhaseKey
            : cooldownSeconds > 0 ? CooldownPhaseKey : MainPhaseKey;

        var remaining = total > 0 ? Math.Max(0, total - elapsed) : (int?)null;
        var progress = total > 0 ? Math.Clamp((double)elapsed / total, 0, 1) : 0;
        return new State(phase, elapsed, remaining, total > 0 ? total : null, progress);
    }

    private static int? ToSeconds(decimal? minutes) =>
        minutes is { } value && value > 0 ? (int)Math.Round(value * 60m) : null;
}
