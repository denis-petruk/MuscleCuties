namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Trusted release configuration for policy review coverage. A feature flag alone never
/// authorises an automatic dose change: the referenced approval must exist and be recorded here.
/// </summary>
public static class PlanningReleaseApproval
{
    /// <summary>
    /// The owner-approved Beta workout policy (docs/release-audit/2026-11-05/WORKOUT_POLICY.md). It is a
    /// product decision, not a clinical sign-off; the in-app notices describe workout estimates and their
    /// limits. Test fixtures use their own references and never appear in production code.
    /// </summary>
    public const string BetaWorkoutPolicyReference = "MC-BETA-WORKOUT-1";

    /// <summary>Date the owner approved <see cref="BetaWorkoutPolicyReference"/>.</summary>
    public static readonly DateTimeOffset BetaWorkoutPolicyApprovedAtUtc =
        new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Active approval record for MC-CYCLE-READINESS-0.3 (owner-approved 2026-11-09 under amendments A-4/A-5).
    /// It covers exactly the two sections that authorise a dose change:
    /// prescribed training and automated progression. Symptom stop logic and injury substitution
    /// deliberately need no approval because they never change a dose. The reference is only
    /// checked in memory; no persisted row, fingerprint or provenance column carries it.
    /// </summary>
    public static readonly PlanningReviewApproval? CycleReadinessPrescription = new(
        TodayCheckInScoreRule.RulesetVersion,
        BetaWorkoutPolicyReference,
        BetaWorkoutPolicyApprovedAtUtc,
        new HashSet<PlanningReviewSection>
        {
            PlanningReviewSection.TrainingPrescriptions,
            PlanningReviewSection.AutomatedProgression
        },
        CycleReadinessPrescriptionRule.PolicyVersion);
}
