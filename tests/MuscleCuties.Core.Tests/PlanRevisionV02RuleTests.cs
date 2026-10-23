using System.Globalization;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PlanRevisionV02RuleTests
{
    private const string SampleUtcTimestamp = "2026-10-23T12:00:00Z";
    private const string TrainingPolicyVersion = "0.2.0";
    private const string ApprovalReference = "synthetic-training-approval";
    private const int ExerciseId = 101;
    private const int PrescribedSets = 2;
    private const int MinimumRepetitions = 8;
    private const int MaximumRepetitions = 10;
    private const decimal PlannedMinutes = 30m;
    private const decimal ActualMinutes = 28m;
    private const long CurrentRevisionNumber = 3;
    private const long ExpectedNextRevisionNumber = 4;
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse(
        SampleUtcTimestamp, CultureInfo.InvariantCulture);
    private static readonly DateOnly Day = DateOnly.FromDateTime(NowUtc.UtcDateTime);

    [Fact]
    public void Rejected_Proposal_Keeps_Exact_Accepted_Revision()
    {
        var current = Current();
        var proposal = Proposal(current);

        var result = PlanRevisionV02Rule.Reject(current, proposal);

        Assert.Equal(PlanChangeStatus.Rejected, result.Status);
        Assert.Same(current, result.Revision);
    }

    [Fact]
    public void Stale_Proposal_Cannot_Overwrite_Newer_Revision()
    {
        var current = Current();
        var proposal = Proposal(current) with { ExpectedRevisionId = Guid.NewGuid() };

        var result = PlanRevisionV02Rule.Accept(current, proposal, Guid.NewGuid(), NowUtc, Approval());

        Assert.Equal(PlanChangeStatus.Stale, result.Status);
        Assert.Same(current, result.Revision);
    }

    [Fact]
    public void Completed_Work_Cannot_Be_Removed_Or_Rewritten()
    {
        var current = Current(completed: true);
        var removed = Proposal(current) with { Sessions = [] };
        var rewritten = Proposal(current) with
        {
            Sessions = [current.Sessions.Single() with { PlannedMinutes = PlannedMinutes + 1m }]
        };

        Assert.Equal(PlanChangeStatus.CompletedWorkConflict,
            PlanRevisionV02Rule.ReviewProposal(current, removed).Status);
        Assert.Equal(PlanChangeStatus.CompletedWorkConflict,
            PlanRevisionV02Rule.ReviewProposal(current, rewritten).Status);
    }

    [Fact]
    public void Finalized_Product_Policy_Can_Be_Explicitly_Accepted_Without_Fabricated_Review_Metadata()
    {
        var current = Current();
        var proposal = Proposal(current);
        var acceptedWithoutReviewRecord = PlanRevisionV02Rule.Accept(current, proposal,
            Guid.NewGuid(), NowUtc, null);

        Assert.Equal(PlanChangeStatus.Accepted, acceptedWithoutReviewRecord.Status);
        Assert.NotNull(acceptedWithoutReviewRecord.Revision);
    }

    [Fact]
    public void Accepted_Change_Gets_New_Revision_And_Frozen_Session_Content()
    {
        var current = Current();
        var editableExercises = new List<PlannedExercisePrescription>
        {
            new(ExerciseId, PrescribedSets, MinimumRepetitions, MaximumRepetitions)
        };
        var proposal = Proposal(current) with
        {
            Sessions = [Session(Guid.NewGuid(), editableExercises)]
        };
        var acceptedRevisionId = Guid.NewGuid();

        var result = PlanRevisionV02Rule.Accept(current, proposal, acceptedRevisionId, NowUtc, Approval());
        editableExercises.Clear();

        Assert.Equal(PlanChangeStatus.Accepted, result.Status);
        Assert.Equal(acceptedRevisionId, result.Revision!.RevisionId);
        Assert.Equal(ExpectedNextRevisionNumber, result.Revision.RevisionNumber);
        Assert.Equal(WorkoutPlanState.Accepted, result.Revision.State);
        Assert.Single(result.Revision.Sessions.Single().Exercises);
        Assert.Equal(NowUtc, result.Revision.AcceptedAtUtc);
    }

    private static WorkoutPlanRevisionSnapshot Current(bool completed = false)
    {
        var planId = Guid.NewGuid();
        return new WorkoutPlanRevisionSnapshot(planId, Guid.NewGuid(), CurrentRevisionNumber,
            WorkoutPlanState.Accepted, TodayCheckInScoreRule.RulesetVersion, TrainingPolicyVersion,
            [Session(Guid.NewGuid(), completed: completed)], NowUtc);
    }

    private static WorkoutPlanChangeProposal Proposal(WorkoutPlanRevisionSnapshot current) =>
        new(Guid.NewGuid(), current.PlanId, current.RevisionId, current.RulesetVersion,
            TrainingPolicyVersion, current.Sessions, NowUtc);

    private static PlannedSessionSnapshot Session(Guid id,
        IReadOnlyList<PlannedExercisePrescription>? exercises = null, bool completed = false) =>
        new(id, Day, PlannedSessionKind.Strength, null,
            exercises ?? [new PlannedExercisePrescription(ExerciseId, PrescribedSets,
                MinimumRepetitions, MaximumRepetitions)],
            new HashSet<ActivityDemandTag> { ActivityDemandTag.UpperBody },
            PlannedMinutes, completed ? PlannedSessionState.Completed : PlannedSessionState.Planned,
            completed ? ActualMinutes : null);

    private static PlanningReviewApproval Approval() =>
        new(TodayCheckInScoreRule.RulesetVersion, ApprovalReference, NowUtc,
            new HashSet<PlanningReviewSection> { PlanningReviewSection.TrainingPrescriptions },
            TrainingPolicyVersion);
}
