using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class InjurySafetyProgressionRuleTests
{
    [Theory]
    [InlineData(0, false, PainProgressionDisposition.AllowProgression, true)]
    [InlineData(2, false, PainProgressionDisposition.AllowProgression, true)]
    [InlineData(3, false, PainProgressionDisposition.AllowProgression, true)]
    [InlineData(2, true, PainProgressionDisposition.MaintainOrRegress, false)] // next-day worsening blocks progression
    [InlineData(4, false, PainProgressionDisposition.MaintainOrRegress, false)]
    [InlineData(6, false, PainProgressionDisposition.MaintainOrRegress, false)]
    [InlineData(7, false, PainProgressionDisposition.BlockAndModify, false)]
    [InlineData(10, false, PainProgressionDisposition.BlockAndModify, false)]
    public void EvaluatePainProgression_AppliesThresholds(
        int pain,
        bool hasNextDayWorsening,
        PainProgressionDisposition expectedDisposition,
        bool expectedAllowsProgression)
    {
        var decision = InjurySafetyProgressionRule.EvaluatePainProgression(pain, hasNextDayWorsening);

        Assert.Equal(expectedDisposition, decision.Disposition);
        Assert.Equal(expectedAllowsProgression, decision.AllowsLoadProgression);
        Assert.Equal(pain >= 7, decision.RequiresExerciseStopOrModification);
    }

    [Fact]
    public void AcuteInjury_BlocksHeavyAndHighImpactMovementsOnAffectedSite()
    {
        var decision = InjurySafetyProgressionRule.EvaluateExerciseSafety(
            InjuryStatus.Acute,
            activeInjurySite: InjuryFlag.Knee,
            isHeavyMovement: true,
            isHighImpactMovement: false,
            exerciseInvolvedSite: InjuryFlag.Knee,
            requestedLoad: 100m);

        Assert.False(decision.IsAllowed);
        Assert.False(decision.IsProgressionAllowed);
        Assert.Equal(InjurySafetyProgressionRule.StatusBlockedAcute, decision.SafetyStatus);
    }

    [Fact]
    public void RecoveringInjury_BlocksProgression()
    {
        var decision = InjurySafetyProgressionRule.EvaluateExerciseSafety(
            InjuryStatus.Recovering,
            activeInjurySite: InjuryFlag.Shoulder,
            isHeavyMovement: false,
            isHighImpactMovement: false,
            exerciseInvolvedSite: InjuryFlag.Shoulder,
            requestedLoad: 40m);

        Assert.True(decision.IsAllowed);
        Assert.False(decision.IsProgressionAllowed);
        Assert.Equal(40m, decision.PrescribedLoad);
        Assert.Equal(InjurySafetyProgressionRule.StatusProgressionBlockedRecovering, decision.SafetyStatus);
    }

    [Fact]
    public void ClearedInjury_CapsBelowPreInjuryMaxLoad()
    {
        var decision = InjurySafetyProgressionRule.EvaluateExerciseSafety(
            InjuryStatus.Cleared,
            activeInjurySite: InjuryFlag.LowBack,
            isHeavyMovement: true,
            isHighImpactMovement: false,
            exerciseInvolvedSite: InjuryFlag.LowBack,
            requestedLoad: 100m,
            preInjuryMaxLoad: 100m);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.IsProgressionAllowed);
        // Capped to 85% of 100m = 85m
        Assert.Equal(85m, decision.PrescribedLoad);
        Assert.Equal(InjurySafetyProgressionRule.StatusLoadCappedCleared, decision.SafetyStatus);
    }
}
