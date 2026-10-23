namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Owner-reported approval is recorded separately from the completeness of prescription rules.</summary>
public static class WorkoutTrainingPolicyV010
{
    public const string PolicyId = "MC-WORKOUT-POLICY";
    public const string Version = "0.1.0";
    public const string ApprovalStatus = "Approved — reported by product owner";
    public const string GenerationUnavailableMessage =
        "Workout plan generation is unavailable until the approved policy prescriptions are configured.";

    public static string? ReviewerDetails => null;
    public static DateTimeOffset? ApprovalDate => null;

    // The owner confirmed approval, but the supplied materials omit the numerical prescriptions.
    public static bool HasCompletePrescriptionRules => false;
    public static bool CanGeneratePlans => HasCompletePrescriptionRules;
}
