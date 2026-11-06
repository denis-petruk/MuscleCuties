using MuscleCuties.Core.Domain.Workout;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Beta 1.0.0 excludes rescheduling. These checks are the negative test for the gate: an otherwise
/// eligible unstarted day must not offer the move action, and the shared rule must fail closed so a
/// hidden control and a rejected request can never disagree.
/// </summary>
public sealed class BetaWorkoutScopeTests
{
    [Fact]
    public void ReschedulingIsGatedForInternalBeta1()
    {
        Assert.False(BetaWorkoutScope.ReschedulingEnabled);
    }

    [Theory]
    [InlineData(false, false)] // the eligible case in Beta 1.1: unstarted training day
    [InlineData(true, false)] // completed session
    [InlineData(false, true)] // rest day
    [InlineData(true, true)]
    public void NoDayOffersReschedulingWhileTheGateIsClosed(bool isCompleted, bool isRestDay)
    {
        Assert.False(BetaWorkoutScope.CanOfferReschedule(isCompleted, isRestDay));
    }

    [Fact]
    public void ADirectScheduleChangeFailsClosedWithAStableCode()
    {
        var failure = Assert.Throws<InvalidOperationException>(BetaWorkoutScope.EnsureRescheduleAllowed);
        Assert.Equal(BetaWorkoutScope.ReschedulingGateCode, failure.Message);
    }
}
