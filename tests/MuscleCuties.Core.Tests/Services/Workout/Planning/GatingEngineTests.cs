using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class GatingEngineTests
{
    private readonly GatingEngine _engine;

    public GatingEngineTests()
    {
        var config = WorkoutPlanningConfig.CreateDefault();
        _engine = new GatingEngine(config);
    }

    private static AdaptiveProfile MakeProfile(
        UserGoal goal = UserGoal.MuscleTone,
        TrainingExperienceLevel exp = TrainingExperienceLevel.Intermediate,
        StrengthTrainingStyle style = StrengthTrainingStyle.ComfortableModerate,
        HashSet<WorkoutActivityType>? selected = null) =>
        new(
            1, goal, exp, 4, 60,
            selected ?? [WorkoutActivityType.HighVolumeStrength, WorkoutActivityType.Hiit, WorkoutActivityType.Running],
            style, Equipment.FullGym, 8000);

    private static DailyInputs MakeInputs(
        double sleep = 7.5, int energy = 3, int pain = 0) =>
        new(DateOnly.FromDateTime(DateTime.Today), sleep, 7, 8000, 8000, energy, pain, false, null);

    private static Readiness MakeReadiness(int score, ReadinessTier tier) =>
        new(score, tier, new Dictionary<string, int>());

    [Theory]
    [InlineData(WorkoutType.Strength)]
    [InlineData(WorkoutType.Cardio)]
    [InlineData(WorkoutType.Recovery)]
    public void ConsecutiveLow_ForcesRestDay(WorkoutType slot)
    {
        var result = _engine.Resolve(
            slot, MakeProfile(), MakeInputs(),
            MakeReadiness(40, ReadinessTier.Low), CyclePhase.Follicular,
            consecutiveLow: 2);

        Assert.Equal(WorkoutActivityType.Yoga, result.Activity);
        Assert.Equal(0, result.SetMultiplier);
    }

    [Fact]
    public void OneLowDay_DoesNotForceRest()
    {
        var result = _engine.Resolve(
            WorkoutType.Strength, MakeProfile(), MakeInputs(),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 1);

        Assert.NotEqual(0.0, result.SetMultiplier);
    }

    [Fact]
    public void RecoverySlot_ReturnsYoga()
    {
        var result = _engine.Resolve(
            WorkoutType.Recovery, MakeProfile(), MakeInputs(),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(WorkoutActivityType.Yoga, result.Activity);
    }

    [Fact]
    public void CardioSlot_LowReadiness_SwappedToRecovery()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio, MakeProfile(), MakeInputs(),
            MakeReadiness(40, ReadinessTier.Low), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(WorkoutActivityType.Yoga, result.Activity);
    }

    [Fact]
    public void Hiit_HighReadiness_Ovulatory_Eligible()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio, MakeProfile(), MakeInputs(sleep: 7.5, energy: 4, pain: 0),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Ovulatory,
            consecutiveLow: 0);

        Assert.Equal(WorkoutActivityType.Hiit, result.Activity);
    }

    [Fact]
    public void Hiit_LutealPhase_Blocked()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio, MakeProfile(), MakeInputs(sleep: 8, energy: 5, pain: 0),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Luteal,
            consecutiveLow: 0);

        Assert.NotEqual(WorkoutActivityType.Hiit, result.Activity);
    }

    [Fact]
    public void Hiit_LowSleep_NotEligible()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio,
            MakeProfile(goal: UserGoal.FatLoss),
            MakeInputs(sleep: 5.0, energy: 5, pain: 0),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.NotEqual(WorkoutActivityType.Hiit, result.Activity);
    }

    [Fact]
    public void Hiit_HighPain_NotEligible()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio,
            MakeProfile(goal: UserGoal.FatLoss),
            MakeInputs(sleep: 8, energy: 5, pain: 2),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.NotEqual(WorkoutActivityType.Hiit, result.Activity);
    }

    [Fact]
    public void Hiit_Menstrual_LowEnergy_NotEligible()
    {
        var result = _engine.Resolve(
            WorkoutType.Cardio,
            MakeProfile(goal: UserGoal.FatLoss),
            MakeInputs(sleep: 8, energy: 3, pain: 0),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Menstrual,
            consecutiveLow: 0);

        Assert.NotEqual(WorkoutActivityType.Hiit, result.Activity);
    }

    [Fact]
    public void StrengthSlot_LowReadiness_ReducedVolumeAndRpe()
    {
        var result = _engine.Resolve(
            WorkoutType.Strength, MakeProfile(), MakeInputs(),
            MakeReadiness(40, ReadinessTier.Low), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(0.7, result.SetMultiplier);
        Assert.Equal(7, result.RpeCap);
    }

    [Fact]
    public void ExpressHard_ModerateReadiness_DowngradedToComfortable()
    {
        var result = _engine.Resolve(
            WorkoutType.Strength,
            MakeProfile(style: StrengthTrainingStyle.ExpressHard, exp: TrainingExperienceLevel.Intermediate),
            MakeInputs(),
            MakeReadiness(60, ReadinessTier.Moderate), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(StrengthTrainingStyle.ComfortableModerate, result.Style);
    }

    [Fact]
    public void Beginner_AlwaysComfortableModerate()
    {
        var result = _engine.Resolve(
            WorkoutType.Strength,
            MakeProfile(style: StrengthTrainingStyle.ExpressHard, exp: TrainingExperienceLevel.Beginner),
            MakeInputs(),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(StrengthTrainingStyle.ComfortableModerate, result.Style);
    }

    [Fact]
    public void RockClimbing_WhenSelected_DoesNotReplaceStrengthSlot()
    {
        var selected = new HashSet<WorkoutActivityType>
        {
            WorkoutActivityType.RockClimbing,
            WorkoutActivityType.HighVolumeStrength
        };

        var result = _engine.Resolve(
            WorkoutType.Strength,
            MakeProfile(selected: selected),
            MakeInputs(),
            MakeReadiness(80, ReadinessTier.High), CyclePhase.Follicular,
            consecutiveLow: 0);

        Assert.Equal(WorkoutActivityType.HighVolumeStrength, result.Activity);
    }
}
