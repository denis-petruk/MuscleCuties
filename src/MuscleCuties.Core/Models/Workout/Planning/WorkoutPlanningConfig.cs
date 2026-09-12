namespace MuscleCuties.Core.Models.Workout.Planning;

public sealed class WorkoutPlanningConfig
{
    public ReadinessConfig Readiness { get; set; } = new();
    public GatingConfig Gating { get; set; } = new();

    public static WorkoutPlanningConfig CreateDefault() => new()
    {
        Readiness = new ReadinessConfig
        {
            SleepLastNight = [[7.5, 30], [6.5, 22], [5.5, 12], [0, 0]],
            Sleep3dAvg = [[7, 15], [6, 8], [0, 0]],
            StepsDeltaPct = new StepsDeltaConfig(),
            EnergyMultiplier = 5,
            Pain = [15, 10, 3, 0],
            PhasePrior = new Dictionary<string, int>
            {
                ["Menstrual"] = -10, ["Luteal"] = -5,
                ["Follicular"] = 0, ["Ovulatory"] = 0, ["Unknown"] = 0
            },
            HighTier = 75,
            ModerateTier = 50
        },
        Gating = new GatingConfig
        {
            HiitMinSleep = 6.5,
            HiitMaxPain = 1,
            HiitMenstrualMinEnergy = 5,
            LowReadinessSetMultiplier = 0.7,
            LowReadinessRpeCap = 7,
            ConsecutiveLowDaysToRest = 2
        }
    };
}

public sealed class ReadinessConfig
{
    public double[][] SleepLastNight { get; set; } = [];
    public double[][] Sleep3dAvg { get; set; } = [];
    public StepsDeltaConfig StepsDeltaPct { get; set; } = new();
    public int EnergyMultiplier { get; set; } = 5;
    public int[] Pain { get; set; } = [15, 10, 3, 0];
    public Dictionary<string, int> PhasePrior { get; set; } = new();
    public int HighTier { get; set; } = 75;
    public int ModerateTier { get; set; } = 50;
}

public sealed class StepsDeltaConfig
{
    public int Normal { get; set; } = 15;
    public int Spike { get; set; } = 5;
    public int Drop { get; set; } = 10;
    public int SpikeThreshold { get; set; } = 40;
    public int DropThreshold { get; set; } = -40;
}

public sealed class GatingConfig
{
    public double HiitMinSleep { get; set; } = 6.5;
    public int HiitMaxPain { get; set; } = 1;
    public int HiitMenstrualMinEnergy { get; set; } = 4;
    public double LowReadinessSetMultiplier { get; set; } = 0.7;
    public int LowReadinessRpeCap { get; set; } = 7;
    public int ConsecutiveLowDaysToRest { get; set; } = 2;
}
