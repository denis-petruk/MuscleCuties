namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// WP02b study-based evidence ranking metadata for exercise variations.
/// Captures longitudinal hypertrophy efficacy, joint friendliness, and movement role (compound vs isolation).
/// </summary>
public sealed record ExerciseEvidenceRanking(
    int HypertrophyEfficacy = 0,
    int JointFriendliness = 0,
    bool IsCompound = true,
    string? PrimaryEvidenceSource = null);
