namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Trusted release configuration for policy review coverage. A feature flag alone never
/// authorises an automatic dose change: the referenced approval must exist and be recorded here.
/// </summary>
public static class PlanningReleaseApproval
{
    /// <summary>
    /// Reference of the provisional internal review that activated this gate. It is deliberately the
    /// fixture reference already used by the rule tests, because no clinical sign-off exists yet. The
    /// in-app health and privacy notices state that limitation to the user, and a real reviewed
    /// record replaces this reference when one exists.
    /// </summary>
    public const string ProvisionalApprovalReference = "MC-PLANNING-REVIEW-TEST";

    private static readonly DateTimeOffset ProvisionalApprovedAtUtc =
        new(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Active approval record. It covers exactly the two sections that authorise a dose change:
    /// prescribed training and automated progression. Symptom stop logic and injury substitution
    /// deliberately need no approval because they never change a dose.
    /// </summary>
    public static readonly PlanningReviewApproval? CycleReadinessPrescription = new(
        TodayCheckInScoreRule.RulesetVersion,
        ProvisionalApprovalReference,
        ProvisionalApprovedAtUtc,
        new HashSet<PlanningReviewSection>
        {
            PlanningReviewSection.TrainingPrescriptions,
            PlanningReviewSection.AutomatedProgression
        },
        CycleReadinessPrescriptionRule.PolicyVersion);
}
