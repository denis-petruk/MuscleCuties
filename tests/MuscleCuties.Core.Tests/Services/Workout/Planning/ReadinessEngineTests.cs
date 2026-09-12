using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class ReadinessEngineTests
{
    private readonly ReadinessEngine _engine;

    public ReadinessEngineTests()
    {
        var config = WorkoutPlanningConfig.CreateDefault();
        _engine = new ReadinessEngine(config);
    }

    private static DailyInputs MakeInputs(
        double sleep = 7.5,
        double sleep3d = 7.0,
        int stepsYesterday = 8000,
        int steps7d = 8000,
        int energy = 3,
        int pain = 0) =>
        new(DateOnly.FromDateTime(DateTime.Today), sleep, sleep3d, stepsYesterday, steps7d, energy, pain, false, null);

    [Theory]
    [InlineData(8.0, 30)]
    [InlineData(7.5, 30)]
    [InlineData(7.0, 22)]
    [InlineData(6.5, 22)]
    [InlineData(6.0, 12)]
    [InlineData(5.5, 12)]
    [InlineData(4.0, 0)]
    [InlineData(0.0, 0)]
    public void SleepScore_BandLookup(double sleepHours, int expectedSleepScore)
    {
        var inputs = MakeInputs(sleep: sleepHours, sleep3d: 0, energy: 0, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(expectedSleepScore, result.Breakdown["sleep"]);
    }

    [Theory]
    [InlineData(8.0, 15)]
    [InlineData(7.0, 15)]
    [InlineData(6.5, 8)]
    [InlineData(6.0, 8)]
    [InlineData(5.0, 0)]
    public void Sleep3dScore_BandLookup(double sleep3d, int expectedScore)
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: sleep3d, energy: 0, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(expectedScore, result.Breakdown["sleep3d"]);
    }

    [Theory]
    [InlineData(8000, 8000, 15)]
    [InlineData(12000, 8000, 5)]
    [InlineData(4000, 8000, 10)]
    [InlineData(0, 0, 15)]
    public void StepsScore_DeltaPct(int stepsYesterday, int steps7d, int expectedScore)
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: 0, stepsYesterday: stepsYesterday, steps7d: steps7d, energy: 0, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(expectedScore, result.Breakdown["steps"]);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    [InlineData(4, 20)]
    [InlineData(5, 25)]
    public void EnergyScore_Multiplied(int energy, int expectedScore)
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: 0, energy: energy, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(expectedScore, result.Breakdown["energy"]);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 10)]
    [InlineData(2, 3)]
    [InlineData(3, 0)]
    public void PainScore_LookupTable(int pain, int expectedScore)
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: 0, energy: 0, pain: pain);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(expectedScore, result.Breakdown["pain"]);
    }

    [Theory]
    [InlineData(CyclePhase.Menstrual, -10)]
    [InlineData(CyclePhase.Luteal, -5)]
    [InlineData(CyclePhase.Follicular, 0)]
    [InlineData(CyclePhase.Ovulatory, 0)]
    [InlineData(CyclePhase.Unknown, 0)]
    public void PhaseScore_Prior(CyclePhase phase, int expectedScore)
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: 0, energy: 0, pain: 3);
        var result = _engine.Compute(inputs, phase);
        Assert.Equal(expectedScore, result.Breakdown["phase"]);
    }

    [Fact]
    public void GoodInputs_HighTier()
    {
        var inputs = MakeInputs(sleep: 8, sleep3d: 7.5, energy: 3, pain: 0);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(ReadinessTier.High, result.Tier);
        Assert.True(result.Score >= 75);
    }

    [Fact]
    public void ModerateInputs_ModerateTier()
    {
        var inputs = MakeInputs(sleep: 6.5, sleep3d: 6.0, energy: 2, pain: 2);
        var result = _engine.Compute(inputs, CyclePhase.Unknown);
        Assert.Equal(ReadinessTier.Moderate, result.Tier);
        Assert.InRange(result.Score, 50, 74);
    }

    [Fact]
    public void PoorInputs_LowTier()
    {
        var inputs = MakeInputs(sleep: 4, sleep3d: 5, energy: 1, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Menstrual);
        Assert.Equal(ReadinessTier.Low, result.Tier);
        Assert.True(result.Score < 50);
    }

    [Fact]
    public void ScoreClampedTo0_100()
    {
        var inputs = MakeInputs(sleep: 0, sleep3d: 0, energy: 0, pain: 3);
        var result = _engine.Compute(inputs, CyclePhase.Menstrual);
        Assert.InRange(result.Score, 0, 100);
    }
}
