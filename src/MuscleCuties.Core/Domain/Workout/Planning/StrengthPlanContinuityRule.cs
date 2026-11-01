using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthContinuityDecision(
    bool IsApplicable,
    int StrengthExerciseCount,
    int WorkingSets,
    string ReasonCode);

/// <summary>
/// Owner rule (2026-11-01): a plan must always be applicable, and a session with an active
/// injury must still contain strength training. Rehab and mobility work are additive; they
/// never satisfy the strength floor and never stand in for it.
/// </summary>
public static class StrengthPlanContinuityRule
{
    public const string StrengthPreserved = "StrengthWorkPreserved";
    public const string SessionWithheldNoApplicableStrength = "SessionWithheldNoApplicableStrength";
    public const int MinimumStrengthExercisesPerSession = 1;
    public const int MinimumWorkingSets = 1;

    /// <summary>
    /// Rehab entries are authored with no primary muscle contribution, so a positive muscle id is
    /// the marker of real strength work in a prescribed session.
    /// </summary>
    public static bool IsStrengthWork(PickedExercise? exercise) =>
        exercise is not null && exercise.PrimaryMuscleId > 0;

    public static bool HasStrengthWork(IReadOnlyList<PickedExercise>? exercises) =>
        exercises is not null && exercises.Any(IsStrengthWork);

    public static StrengthContinuityDecision Evaluate(IReadOnlyList<PickedExercise>? exercises)
    {
        if (exercises is null)
            return new(false, 0, 0, SessionWithheldNoApplicableStrength);

        var strength = exercises.Where(IsStrengthWork).ToArray();
        var workingSets = strength.Sum(exercise => exercise.Sets);
        var applicable = strength.Length >= MinimumStrengthExercisesPerSession &&
                         workingSets >= MinimumWorkingSets;
        return new(applicable, strength.Length, workingSets,
            applicable ? StrengthPreserved : SessionWithheldNoApplicableStrength);
    }

    /// <summary>
    /// Returns the session only when it satisfies the strength floor. A session that would consist
    /// of rehab or mobility work alone is withheld instead of being presented as strength training.
    /// </summary>
    public static PrescribedSession? EnsureApplicable(PrescribedSession? session) =>
        session is not null && Evaluate(session.Exercises).IsApplicable ? session : null;
}
