using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PhaseReviewRuleTests
{
    private const int ReviewYear = 2026;
    private const int ReviewMonth = 10;
    private const int ReviewDay = 24;
    private const int ReviewHourUtc = 12;
    private const int ScheduledHourUtc = 15;
    private const int BaselineDiscomfort = 2;
    private const int BaselineEnergy = 3;
    private const int ChangedBaselineEnergy = 4;
    private static readonly DateOnly Today = new(ReviewYear, ReviewMonth, ReviewDay);
    private static readonly DateTimeOffset ReviewAtUtc = new(
        ReviewYear, ReviewMonth, ReviewDay, ReviewHourUtc, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CyclePhase.Menstrual)]
    [InlineData(CyclePhase.Follicular)]
    [InlineData(CyclePhase.Ovulatory)]
    [InlineData(CyclePhase.Luteal)]
    public void Confirmed_Phase_Only_Identifies_Upcoming_Candidates(CyclePhase phase)
    {
        var future = Session(1);
        var input = Input(phase, sessions: [future]);

        var result = PhaseReviewRule.Evaluate(input);

        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview, result.Status);
        Assert.Equal([future.SessionId], result.EligibleSessionIds);
        Assert.Equal(Today.AddDays(PhaseReviewRule.FollowingCalendarDates),
            result.HorizonEnd);
        Assert.Same(future, input.Sessions.Single());
    }

    [Fact]
    public void Estimated_Unknown_And_Disabled_Context_Cannot_Reach_Adjustment_Rules()
    {
        var input = Input(sessions: [Session(1)]);

        Assert.Equal(PhaseReviewGateStatus.NeedsPhaseConfirmation,
            PhaseReviewRule.Evaluate(input with
            {
                Context = input.Context with { Provenance = PhaseReviewProvenance.Estimated }
            }).Status);
        Assert.Equal(PhaseReviewGateStatus.UnknownPhase,
            PhaseReviewRule.Evaluate(input with
            {
                Context = input.Context with { Phase = CyclePhase.Unknown,
                    Provenance = PhaseReviewProvenance.Unknown }
            }).Status);
        Assert.Equal(PhaseReviewGateStatus.TrackingDisabled,
            PhaseReviewRule.Evaluate(input with
            {
                Context = input.Context with { Provenance = PhaseReviewProvenance.TrackingDisabled }
            }).Status);
        Assert.Equal(PhaseReviewGateStatus.TrackingDisabled,
            PhaseReviewRule.Evaluate(input with { PhaseReviewPromptsEnabled = false }).Status);
    }

    [Fact]
    public void Current_Symptoms_Or_Restrictions_Prevent_Baseline_Based_Adjustments()
    {
        var input = Input(sessions: [Session(1)]) with
        {
            HasCurrentSymptomsOrRestrictions = true
        };

        var result = PhaseReviewRule.Evaluate(input);

        Assert.Equal(PhaseReviewGateStatus.CurrentSafetyReviewRequired, result.Status);
        Assert.Empty(result.EligibleSessionIds);
    }

    [Fact]
    public void Missing_Or_Invalid_Baseline_Does_Not_Assign_A_Tier()
    {
        var input = Input(sessions: [Session(1)]);

        Assert.Equal(PhaseReviewGateStatus.MissingBaseline,
            PhaseReviewRule.Evaluate(input with { Baseline = null }).Status);
        Assert.Equal(PhaseReviewGateStatus.MissingBaseline,
            PhaseReviewRule.Evaluate(input with
            {
                BaselineScale = PhaseBaselineScale.Unknown
            }).Status);
        Assert.Equal(PhaseReviewGateStatus.MissingBaseline,
            PhaseReviewRule.Evaluate(input with
            {
                Baseline = new PhaseBaseline(BaselineDiscomfort,
                    PhaseReviewRule.MaximumEnergyRating + 1)
            }).Status);
    }

    [Fact]
    public void Correction_And_Baseline_Change_Can_Trigger_A_Fresh_Review()
    {
        var input = Input(sessions: [Session(1)]);
        var previouslyReviewed = input with
        {
            PreviousReviewedPhase = input.Context.Phase,
            PreviousReviewedContextId = input.Context.ContextId,
            PreviousReviewedBaseline = input.Baseline
        };

        Assert.Equal(PhaseReviewGateStatus.AlreadyReviewed,
            PhaseReviewRule.Evaluate(previouslyReviewed).Status);
        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview,
            PhaseReviewRule.Evaluate(previouslyReviewed with
            {
                Context = input.Context with { ContextId = Guid.NewGuid() }
            }).Status);
        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview,
            PhaseReviewRule.Evaluate(previouslyReviewed with
            {
                Baseline = new PhaseBaseline(BaselineDiscomfort, ChangedBaselineEnergy)
            }).Status);
    }

    [Fact]
    public void Declined_Context_Is_Suppressed_Until_Explicit_Request()
    {
        var input = Input(sessions: [Session(1)]);
        var key = PhaseReviewRule.Evaluate(input).DismissalKey!;
        input = input with { Dismissals = new HashSet<PhaseReviewDismissalKey> { key } };

        Assert.Equal(PhaseReviewGateStatus.Dismissed,
            PhaseReviewRule.Evaluate(input).Status);
        Assert.Equal(PhaseReviewGateStatus.Dismissed,
            PhaseReviewRule.Evaluate(input with { SourceRevisionId = Guid.NewGuid() }).Status);
        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview,
            PhaseReviewRule.Evaluate(input with { ExplicitReviewRequested = true }).Status);
    }

    [Fact]
    public void Horizon_Stops_Before_Estimated_Transition_And_Plan_End()
    {
        var input = Input(sessions: [Session(1), Session(2), Session(3)]);
        input = input with
        {
            PlanEndDate = Today.AddDays(2),
            Context = input.Context with { NextEstimatedTransitionDate = Today.AddDays(2) }
        };

        var result = PhaseReviewRule.Evaluate(input);

        Assert.Equal(Today.AddDays(1), result.HorizonEnd);
        Assert.Single(result.EligibleSessionIds);
        Assert.Equal(input.Sessions[0].SessionId, result.EligibleSessionIds[0]);
    }

    [Fact]
    public void Started_Recorded_Completed_Partial_And_Skipped_Sessions_Are_Protected()
    {
        var sessions = new[]
        {
            Session(1, state: PlannedSessionState.Completed),
            Session(1, state: PlannedSessionState.PartiallyCompleted),
            Session(1, state: PlannedSessionState.Skipped),
            Session(1, hasStarted: true),
            Session(1, hasRecordedWork: true),
            Session(1)
        };

        var result = PhaseReviewRule.Evaluate(Input(sessions: sessions));

        Assert.Equal([sessions[^1].SessionId], result.EligibleSessionIds);
    }

    [Fact]
    public void Sessions_At_Or_Before_Review_Time_Are_Not_Candidates()
    {
        var atReview = Session(0) with { ScheduledStartUtc = ReviewAtUtc };
        var beforeReview = Session(0) with { ScheduledStartUtc = ReviewAtUtc.AddMinutes(-1) };
        var later = Session(0);

        var result = PhaseReviewRule.Evaluate(Input(sessions: [atReview, beforeReview, later]));

        Assert.Equal([later.SessionId], result.EligibleSessionIds);
    }

    [Fact]
    public async Task Use_Case_Reads_Once_And_Does_Not_Save_A_Plan()
    {
        var source = new SyntheticSource(Input(sessions: [Session(1)]));
        var useCase = new ReviewActivePlanPhaseV010UseCase(source);

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview, result.Status);
        Assert.Equal(1, source.LoadCount);
    }

    private static PhaseReviewGateInput Input(CyclePhase phase = CyclePhase.Menstrual,
        IReadOnlyList<PhaseReviewSession>? sessions = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Today.AddDays(PhaseReviewRule.FollowingCalendarDates),
            ReviewAtUtc, Today,
            new PhaseReviewContext(Guid.NewGuid(), phase,
                PhaseReviewProvenance.UserConfirmed, null),
            new PhaseBaseline(BaselineDiscomfort, BaselineEnergy),
            PhaseBaselineScale.QuizMappedOneToFive,
            null, null, null, false, true,
            sessions ?? Array.Empty<PhaseReviewSession>(),
            new HashSet<PhaseReviewDismissalKey>());

    private static PhaseReviewSession Session(int daysAfterToday,
        PlannedSessionState state = PlannedSessionState.Planned,
        bool hasStarted = false, bool hasRecordedWork = false) =>
        new(Guid.NewGuid(), Today.AddDays(daysAfterToday),
            ReviewAtUtc.AddDays(daysAfterToday).AddHours(ScheduledHourUtc - ReviewHourUtc),
            state, hasStarted, hasRecordedWork);

    private sealed class SyntheticSource(PhaseReviewGateInput input) : IActivePlanPhaseReviewSourceV010
    {
        public int LoadCount { get; private set; }

        public Task<PhaseReviewGateInput?> LoadAsync(Guid userId,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult<PhaseReviewGateInput?>(input);
        }
    }
}
