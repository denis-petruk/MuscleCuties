using System.Globalization;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ProgressionReviewV02RuleTests
{
    private const string SampleUtcTimestamp = "2026-10-23T12:00:00Z";
    private const string PolicyVersion = "synthetic-reviewed-policy";
    private const string ExerciseVersion = "synthetic-exercise-version";
    private const string ApprovalReference = "synthetic-approval";
    private const int ExerciseId = 101;
    private const int RequiredQualifyingSessions = 2;
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse(
        SampleUtcTimestamp, CultureInfo.InvariantCulture);

    [Fact]
    public void Missing_Approval_Or_Policy_Prevents_Progression()
    {
        var context = Context();

        Assert.Equal(ProgressionReviewState.NeedsProgrammingReview,
            ProgressionReviewV02Rule.Evaluate(context with { Approval = null }).State);
        Assert.Equal(ProgressionReviewState.NeedsProgrammingReview,
            ProgressionReviewV02Rule.Evaluate(context with { Policy = null }).State);
        Assert.Equal(ProgressionReviewState.NeedsProgrammingReview,
            ProgressionReviewV02Rule.Evaluate(context with
            {
                Approval = Approval() with { ReviewedTrainingPolicyVersion = "other-version" }
            }).State);
    }

    [Fact]
    public void Empty_Or_Duplicate_History_Is_Insufficient()
    {
        var evidence = Evidence();

        Assert.Equal(ProgressionReviewState.InsufficientHistory,
            ProgressionReviewV02Rule.Evaluate(Context() with { History = [] }).State);
        Assert.Equal(ProgressionReviewState.InsufficientHistory,
            ProgressionReviewV02Rule.Evaluate(Context() with { History = [evidence, evidence] }).State);
    }

    [Fact]
    public void Current_Limitation_And_Incomplete_Latest_Work_Block_Eligibility()
    {
        var history = QualifyingHistory();
        var withCurrentLimitation = Context() with
        {
            History = history,
            HasCurrentPainOrLimitation = true,
            UserConfirmed = true
        };
        var latestIncomplete = Context() with
        {
            History = [history[0] with { HasCompletePerformanceData = false }, history[1]],
            UserConfirmed = true
        };

        Assert.Equal(ProgressionReviewState.BlockedByCurrentLimitation,
            ProgressionReviewV02Rule.Evaluate(withCurrentLimitation).State);
        Assert.Equal(ProgressionReviewState.Maintain,
            ProgressionReviewV02Rule.Evaluate(latestIncomplete).State);
    }

    [Fact]
    public void Substitution_Or_Pain_Breaks_Consecutive_Qualifying_History()
    {
        var history = QualifyingHistory();

        Assert.Equal(ProgressionReviewState.Maintain,
            ProgressionReviewV02Rule.Evaluate(Context() with
            {
                History = [history[0] with { WasSubstituted = true }, history[1]]
            }).State);
        Assert.Equal(ProgressionReviewState.Maintain,
            ProgressionReviewV02Rule.Evaluate(Context() with
            {
                History = [history[0] with { HasPainOrLimitation = true }, history[1]]
            }).State);
    }

    [Fact]
    public void Qualifying_History_Requires_Explicit_User_Confirmation()
    {
        var context = Context() with { History = QualifyingHistory() };

        var review = ProgressionReviewV02Rule.Evaluate(context);
        var confirmed = ProgressionReviewV02Rule.Evaluate(context with { UserConfirmed = true });

        Assert.Equal(ProgressionReviewState.EligibleForReview, review.State);
        Assert.Equal(RequiredQualifyingSessions, review.QualifyingConsecutiveSessions);
        Assert.Equal(ProgressionReviewState.UserConfirmedProgression, confirmed.State);
    }

    private static ProgressionReviewContext Context() =>
        new(ExerciseId, ExerciseVersion, null, false, false,
            new ProgressionReviewPolicy(PolicyVersion, RequiredQualifyingSessions), Approval());

    private static PlanningReviewApproval Approval() =>
        new(TodayCheckInScoreRule.RulesetVersion, ApprovalReference, NowUtc,
            new HashSet<PlanningReviewSection>
            {
                PlanningReviewSection.TrainingPrescriptions,
                PlanningReviewSection.AutomatedProgression
            }, PolicyVersion);

    private static ProgressionSessionEvidence Evidence() =>
        new(Guid.NewGuid(), ExerciseId, ExerciseVersion, NowUtc, true, true, false, false);

    private static ProgressionSessionEvidence[] QualifyingHistory() =>
        [Evidence(), Evidence() with { CompletedAtUtc = NowUtc.AddDays(-1) }];
}
