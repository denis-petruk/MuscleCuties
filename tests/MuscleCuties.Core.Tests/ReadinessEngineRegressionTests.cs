using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class ReadinessEngineRegressionTests
{
    [Fact]
    public void Missing_History_Does_Not_Create_Sleep_Or_Steps_Credit()
    {
        var engine = new ReadinessEngine(WorkoutPlanningConfig.CreateDefault());
        var inputs = Inputs(sleepHours: 7.5, sleepAverage: 0, steps: 0, stepsAverage: 0);

        var result = engine.Compute(inputs, CyclePhase.Follicular);

        Assert.Equal(0, result.Breakdown["sleep3d"]);
        Assert.Equal(0, result.Breakdown["steps"]);
        Assert.Equal(60, result.Score);
        Assert.Equal(ReadinessTier.Moderate, result.Tier);
    }

    [Fact]
    public void Invalid_And_Negative_Input_Returns_A_Bounded_Record()
    {
        var config = WorkoutPlanningConfig.CreateDefault();
        config.Readiness.SleepLastNight = [[], [double.NaN, 30], [-1, 30]];
        config.Readiness.Sleep3dAvg = [];
        config.Readiness.Pain = [];
        config.Readiness.StepsDeltaPct = null!;
        var engine = new ReadinessEngine(config);

        var result = engine.Compute(Inputs(double.NegativeInfinity, -3, -1, 0), CyclePhase.Unknown);

        Assert.InRange(result.Score, 0, 100);
        Assert.Equal(0, result.Breakdown["sleep"]);
        Assert.Equal(0, result.Breakdown["sleep3d"]);
        Assert.Equal(0, result.Breakdown["steps"]);
        Assert.Equal(0, result.Breakdown["pain"]);
    }

    [Fact]
    public void Missing_Config_Returns_A_Safe_Default()
    {
        var result = new ReadinessEngine(null).Compute(Inputs(7.5, 7, 8000, 8000), CyclePhase.Follicular);

        Assert.Equal(0, result.Score);
        Assert.Equal(ReadinessTier.Low, result.Tier);
        Assert.All(result.Breakdown.Values, value => Assert.Equal(0, value));
    }

    private static DailyInputs Inputs(double sleepHours, double sleepAverage, int steps, int stepsAverage) =>
        new(DateOnly.FromDateTime(DateTime.Today), sleepHours, sleepAverage, steps, stepsAverage,
            Energy: 3, Pain: 0, Bloating: false, WeightKg: null);
}
