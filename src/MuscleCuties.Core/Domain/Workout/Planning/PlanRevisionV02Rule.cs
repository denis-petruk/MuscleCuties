using System.Collections.Frozen;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum WorkoutPlanState
{
    Draft,
    Proposed,
    Accepted,
    InProgress,
    Completed,
    PartiallyCompleted,
    Skipped
}

public enum PlannedSessionState
{
    Planned,
    Completed,
    PartiallyCompleted,
    Skipped
}

public enum PlannedSessionKind
{
    Strength,
    Cardio,
    Mobility,
    Recovery
}

public enum PlanChangeStatus
{
    Ready,
    Accepted,
    Rejected,
    Stale,
    Invalid,
    CompletedWorkConflict,
    NeedsProgrammingReview
}

public sealed record PlannedExercisePrescription(
    int ExerciseId,
    int Sets,
    int MinimumRepetitions,
    int MaximumRepetitions);

public sealed record PlannedSessionSnapshot(
    Guid SessionId,
    DateOnly Date,
    PlannedSessionKind Kind,
    int? ActivityId,
    IReadOnlyList<PlannedExercisePrescription> Exercises,
    IReadOnlySet<ActivityDemandTag> DemandTags,
    decimal? PlannedMinutes,
    PlannedSessionState State,
    decimal? ActualMinutes);

public sealed record WorkoutPlanRevisionSnapshot(
    Guid PlanId,
    Guid RevisionId,
    long RevisionNumber,
    WorkoutPlanState State,
    string RulesetVersion,
    string? TrainingPolicyVersion,
    IReadOnlyList<PlannedSessionSnapshot> Sessions,
    DateTimeOffset? AcceptedAtUtc);

public sealed record WorkoutPlanChangeProposal(
    Guid ProposalId,
    Guid PlanId,
    Guid ExpectedRevisionId,
    string RulesetVersion,
    string? TrainingPolicyVersion,
    IReadOnlyList<PlannedSessionSnapshot> Sessions,
    DateTimeOffset CreatedAtUtc);

public sealed record PlanChangeResult(PlanChangeStatus Status, WorkoutPlanRevisionSnapshot? Revision);

/// <summary>
/// Validates immutable proposals and accepted revisions. Persistence must compare ExpectedRevisionId
/// and write the new revision in one transaction; stale proposals are never rebased automatically.
/// </summary>
public static class PlanRevisionV02Rule
{
    private const long InitialRevisionNumber = 1;

    public static PlanChangeResult ReviewProposal(
        WorkoutPlanRevisionSnapshot? current,
        WorkoutPlanChangeProposal? proposal)
    {
        if (proposal is null || proposal.ProposalId == Guid.Empty || proposal.PlanId == Guid.Empty ||
            proposal.CreatedAtUtc.Offset != TimeSpan.Zero ||
            proposal.RulesetVersion != TodayCheckInScoreRule.RulesetVersion ||
            !ValidSessions(proposal.Sessions))
            return new PlanChangeResult(PlanChangeStatus.Invalid, current);

        if (current is null)
        {
            if (proposal.ExpectedRevisionId != Guid.Empty)
                return new PlanChangeResult(PlanChangeStatus.Stale, null);
            return new PlanChangeResult(PlanChangeStatus.Ready, null);
        }

        if (current.PlanId != proposal.PlanId || current.RevisionId != proposal.ExpectedRevisionId ||
            current.RevisionNumber < InitialRevisionNumber ||
            current.RulesetVersion != proposal.RulesetVersion)
            return new PlanChangeResult(PlanChangeStatus.Stale, current);
        if (current.Sessions is null || !CompletedWorkPreserved(current.Sessions, proposal.Sessions))
            return new PlanChangeResult(PlanChangeStatus.CompletedWorkConflict, current);

        return new PlanChangeResult(PlanChangeStatus.Ready, current);
    }

    public static PlanChangeResult Accept(
        WorkoutPlanRevisionSnapshot? current,
        WorkoutPlanChangeProposal? proposal,
        Guid acceptedRevisionId,
        DateTimeOffset acceptedAtUtc,
        PlanningReviewApproval? approval)
    {
        var review = ReviewProposal(current, proposal);
        if (review.Status != PlanChangeStatus.Ready)
            return review;
        if (acceptedRevisionId == Guid.Empty || acceptedAtUtc.Offset != TimeSpan.Zero ||
            acceptedAtUtc < proposal!.CreatedAtUtc || acceptedRevisionId == proposal.ProposalId ||
            acceptedRevisionId == current?.RevisionId)
            return new PlanChangeResult(PlanChangeStatus.Invalid, current);

        var hasPrescriptions = proposal.Sessions.Any(session => session.Exercises.Count > 0 ||
            session.ActivityId.HasValue || session.PlannedMinutes is > 0);
        if (hasPrescriptions && (string.IsNullOrWhiteSpace(proposal.TrainingPolicyVersion) ||
                                 !HasTrainingApproval(approval, proposal.RulesetVersion,
                                     proposal.TrainingPolicyVersion)))
            return new PlanChangeResult(PlanChangeStatus.NeedsProgrammingReview, current);

        if (current?.RevisionNumber == long.MaxValue)
            return new PlanChangeResult(PlanChangeStatus.Invalid, current);

        var frozenSessions = Array.AsReadOnly(proposal.Sessions
            .Select(session => session with
            {
                Exercises = Array.AsReadOnly(session.Exercises.ToArray()),
                DemandTags = session.DemandTags.ToFrozenSet()
            })
            .ToArray());

        var accepted = new WorkoutPlanRevisionSnapshot(
            proposal.PlanId,
            acceptedRevisionId,
            current is null ? InitialRevisionNumber : checked(current.RevisionNumber + 1),
            WorkoutPlanState.Accepted,
            proposal.RulesetVersion,
            proposal.TrainingPolicyVersion,
            frozenSessions,
            acceptedAtUtc);
        return new PlanChangeResult(PlanChangeStatus.Accepted, accepted);
    }

    public static PlanChangeResult Reject(WorkoutPlanRevisionSnapshot? current,
        WorkoutPlanChangeProposal? proposal)
    {
        var review = ReviewProposal(current, proposal);
        return review.Status == PlanChangeStatus.Ready
            ? new PlanChangeResult(PlanChangeStatus.Rejected, current)
            : review;
    }

    private static bool ValidSessions(IReadOnlyList<PlannedSessionSnapshot>? sessions) =>
        sessions is not null && sessions.All(session =>
            session is not null && session.SessionId != Guid.Empty && Enum.IsDefined(session.Kind) &&
            Enum.IsDefined(session.State) && session.Exercises is not null && session.DemandTags is not null &&
            session.DemandTags.All(tag => Enum.IsDefined(tag)) &&
            session.ActivityId is not <= 0 &&
            session.PlannedMinutes is not < 0 && session.ActualMinutes is not < 0 &&
            session.Exercises.All(exercise => exercise is not null && exercise.ExerciseId > 0 &&
                                              exercise.Sets > 0 && exercise.MinimumRepetitions > 0 &&
                                              exercise.MaximumRepetitions >= exercise.MinimumRepetitions) &&
            (session.State is PlannedSessionState.Completed or PlannedSessionState.PartiallyCompleted) ==
            session.ActualMinutes.HasValue) &&
        sessions.Select(session => session.SessionId).Distinct().Count() == sessions.Count;

    private static bool CompletedWorkPreserved(
        IReadOnlyList<PlannedSessionSnapshot> existing,
        IReadOnlyList<PlannedSessionSnapshot> proposed)
    {
        foreach (var completed in existing.Where(session =>
                     session.State is PlannedSessionState.Completed or PlannedSessionState.PartiallyCompleted))
        {
            var replacement = proposed.SingleOrDefault(session => session.SessionId == completed.SessionId);
            if (replacement is null || !SameSession(completed, replacement))
                return false;
        }

        return true;
    }

    private static bool SameSession(PlannedSessionSnapshot first, PlannedSessionSnapshot second) =>
        first.Date == second.Date && first.Kind == second.Kind && first.ActivityId == second.ActivityId &&
        first.PlannedMinutes == second.PlannedMinutes && first.State == second.State &&
        first.ActualMinutes == second.ActualMinutes && first.Exercises.SequenceEqual(second.Exercises) &&
        first.DemandTags.SetEquals(second.DemandTags);

    private static bool HasTrainingApproval(PlanningReviewApproval? approval, string rulesetVersion,
        string trainingPolicyVersion) =>
        approval is not null && approval.RulesetVersion == rulesetVersion &&
        approval.ReviewedTrainingPolicyVersion == trainingPolicyVersion &&
        !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
        approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
        approval.CoveredSections?.Contains(PlanningReviewSection.TrainingPrescriptions) == true;
}
