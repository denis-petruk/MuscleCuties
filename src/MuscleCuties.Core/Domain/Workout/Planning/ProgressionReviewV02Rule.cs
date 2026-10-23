namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ProgressionReviewState
{
    NeedsProgrammingReview,
    InsufficientHistory,
    Maintain,
    BlockedByCurrentLimitation,
    EligibleForReview,
    UserConfirmedProgression
}

/// <summary>Only an approved server-owned training policy may provide this requirement.</summary>
public sealed record ProgressionReviewPolicy(
    string Version,
    int RequiredConsecutiveQualifyingSessions);

public sealed record ProgressionSessionEvidence(
    Guid SessionId,
    int ExerciseId,
    string ExerciseVersion,
    DateTimeOffset CompletedAtUtc,
    bool HasCompletePerformanceData,
    bool MetReviewedTargets,
    bool HasPainOrLimitation,
    bool WasSubstituted);

public sealed record ProgressionReviewContext(
    int ExerciseId,
    string ExerciseVersion,
    IReadOnlyList<ProgressionSessionEvidence>? History,
    bool HasCurrentPainOrLimitation,
    bool UserConfirmed,
    ProgressionReviewPolicy? Policy,
    PlanningReviewApproval? Approval);

public sealed record ProgressionReviewDecision(
    ProgressionReviewState State,
    int QualifyingConsecutiveSessions);

/// <summary>
/// Determines whether progression may be reviewed, without changing load, sets, or a saved plan.
/// The caller must derive MetReviewedTargets from the approved policy and actual session data.
/// </summary>
public static class ProgressionReviewV02Rule
{
    private const int NoQualifyingSessions = 0;

    public static ProgressionReviewDecision Evaluate(ProgressionReviewContext? context)
    {
        if (context is null || !HasApprovedPolicy(context))
            return new ProgressionReviewDecision(ProgressionReviewState.NeedsProgrammingReview,
                NoQualifyingSessions);

        if (context.HasCurrentPainOrLimitation)
            return new ProgressionReviewDecision(ProgressionReviewState.BlockedByCurrentLimitation,
                NoQualifyingSessions);

        var history = context.History;
        if (history is null || history.Count == 0)
            return new ProgressionReviewDecision(ProgressionReviewState.InsufficientHistory,
                NoQualifyingSessions);

        if (history.Any(item => item is null || item.SessionId == Guid.Empty ||
                                item.ExerciseId != context.ExerciseId ||
                                item.ExerciseVersion != context.ExerciseVersion ||
                                item.CompletedAtUtc.Offset != TimeSpan.Zero) ||
            history.Select(item => item.SessionId).Distinct().Count() != history.Count)
            return new ProgressionReviewDecision(ProgressionReviewState.InsufficientHistory,
                NoQualifyingSessions);

        var ordered = history.OrderByDescending(item => item.CompletedAtUtc).ToArray();
        var qualifyingCount = ordered.TakeWhile(item =>
                item.HasCompletePerformanceData && item.MetReviewedTargets &&
                !item.HasPainOrLimitation && !item.WasSubstituted)
            .Count();

        if (qualifyingCount == NoQualifyingSessions)
            return new ProgressionReviewDecision(ProgressionReviewState.Maintain, qualifyingCount);
        if (qualifyingCount < context.Policy!.RequiredConsecutiveQualifyingSessions)
            return new ProgressionReviewDecision(ProgressionReviewState.InsufficientHistory,
                qualifyingCount);

        return new ProgressionReviewDecision(context.UserConfirmed
            ? ProgressionReviewState.UserConfirmedProgression
            : ProgressionReviewState.EligibleForReview, qualifyingCount);
    }

    private static bool HasApprovedPolicy(ProgressionReviewContext context)
    {
        var policy = context.Policy;
        var approval = context.Approval;
        return context.ExerciseId > 0 && !string.IsNullOrWhiteSpace(context.ExerciseVersion) &&
               policy is { RequiredConsecutiveQualifyingSessions: > 0 } &&
               !string.IsNullOrWhiteSpace(policy.Version) &&
               approval is not null &&
               approval.RulesetVersion == TodayCheckInScoreRule.RulesetVersion &&
               approval.ReviewedTrainingPolicyVersion == policy.Version &&
               !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
               approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
               approval.CoveredSections?.Contains(PlanningReviewSection.TrainingPrescriptions) == true &&
               approval.CoveredSections.Contains(PlanningReviewSection.AutomatedProgression);
    }
}
