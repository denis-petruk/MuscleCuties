using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout;

/// <summary>
/// Gate 78: how a session exercise is grouped and which metrics it collects. A name alone cannot
/// decide that a walk is cardio: loaded walking movements (walking lunge, banded lateral walk,
/// farmer's walk) are prescribed in sets and reps and belong to the strength section. Only a
/// duration-based walk is conditioning work. Misclassifying one exercise splits a strength session
/// into two activities, so the athlete logs every exercise they were shown and the server still
/// rejects the finish because the day is short of the hidden second section.
/// </summary>
public static class WorkoutActivityTagRule
{
    public const string Strength = "STRENGTH";
    public const string Cardio = "CARDIO";
    public const string Recovery = "RECOVERY";
    public const string Rest = "REST";

    private static readonly string[] RecoveryTerms =
        ["yoga", "vinyasa", "mobility", "recovery", "breathing", "pilates", "stretch"];

    // "walk" is deliberately absent: loaded walks are strength work unless the prescription is timed.
    private static readonly string[] CardioTerms =
        ["run", "jog", "sprint", "tempo", "cycle", "cycling", "ride", "bike", "swim", "pool", "hiit",
            "interval", "dance"];

    private static readonly string[] WalkTerms = ["walk"];

    private static readonly string[] EnduranceTerms =
        ["plank", "copenhagen", "bike", "cycle", "cycling", "ride", "run", "jog", "sprint", "tempo",
            "swim", "pool", "hiit", "interval", "dance", "yoga", "vinyasa", "mobility", "recovery",
            "breathing", "pilates"];

    public static string Classify(string workoutType, string? exerciseName, int? durationSeconds)
    {
        var name = exerciseName ?? string.Empty;
        if (ContainsAny(name, RecoveryTerms)) return Recovery;
        if (ContainsAny(name, CardioTerms)) return Cardio;
        var isCardioDay = string.Equals(workoutType, nameof(WorkoutType.Cardio), StringComparison.OrdinalIgnoreCase);
        if (ContainsAny(name, WalkTerms) && (durationSeconds is > 0 || isCardioDay)) return Cardio;
        if (string.Equals(workoutType, nameof(WorkoutType.Recovery), StringComparison.OrdinalIgnoreCase))
            return Recovery;
        if (isCardioDay && durationSeconds is > 0) return Cardio;
        return Strength;
    }

    public static bool UsesEnduranceMetrics(string? exerciseName, int? durationSeconds)
    {
        if (durationSeconds is > 0) return true;
        return ContainsAny(exerciseName ?? string.Empty, EnduranceTerms);
    }

    public static bool IsTimedWalk(string? exerciseName, int? durationSeconds) =>
        durationSeconds is > 0 && ContainsAny(exerciseName ?? string.Empty, WalkTerms);

    private static bool ContainsAny(string value, string[] terms)
    {
        // Match activity word stems (run/running), never fragments inside strength names (crunch).
        var words = value.Split(value.Where(character => !char.IsLetterOrDigit(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => terms.Any(term => word.StartsWith(term, StringComparison.OrdinalIgnoreCase)));
    }
}
