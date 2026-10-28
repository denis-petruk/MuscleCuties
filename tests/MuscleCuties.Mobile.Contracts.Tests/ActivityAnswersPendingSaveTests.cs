using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class ActivityAnswersPendingSaveTests
{
    [Fact]
    public void RetryKeepsOriginalVersionAndChallengeWhileCheckingAnswerContent()
    {
        var challenge = Guid.NewGuid();
        var original = new SaveActivityAnswersRequest(4,
            [new ActivityEligibilityInputV020(WorkoutActivityDto.Swimming, HasSwimmingFacilityAccess: true)],
            new HiitEligibilityEvidenceInputV020(ExistingClearanceScreeningSatisfied: true))
        { HiitCaptureChallengeId = challenge, HiitUnderlyingCardioActivity = WorkoutActivityDto.Swimming };
        var pending = new ActivityAnswersPendingSave(original, true);
        var sameAnswers = original with { ExpectedVersion = 5, HiitCaptureChallengeId = null };

        Assert.True(pending.Matches(sameAnswers, true));
        Assert.Same(original, pending.Request);
        Assert.Equal(challenge, pending.Request.HiitCaptureChallengeId);
        Assert.Equal(4, pending.Request.ExpectedVersion);
        Assert.False(pending.Matches(sameAnswers with
        {
            HiitEligibilityEvidence = new HiitEligibilityEvidenceInputV020(ExistingClearanceScreeningSatisfied: false)
        }, true));
        Assert.False(pending.Matches(sameAnswers, false));
    }
}
