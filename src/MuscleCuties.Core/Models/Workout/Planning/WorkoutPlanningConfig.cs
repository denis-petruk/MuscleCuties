namespace MuscleCuties.Core.Models.Workout.Planning;

public sealed class WorkoutPlanningConfig
{
    private const int GatingHiitMenstrualMinimumEnergy = 5;

    private static readonly double[][] DefaultSleepLastNightBands =
        [[7.5, 30], [6.5, 22], [5.5, 12], [0, 0]];

    private static readonly double[][] DefaultSleep3dAvgBands =
        [[7, 15], [6, 8], [0, 0]];

    private static readonly Dictionary<string, int> DefaultPhasePriors = new()
    {
        ["Menstrual"] = -10,
        ["Luteal"] = -5,
        ["Follicular"] = 0,
        ["Ovulatory"] = 0,
        ["Unknown"] = 0
    };

    public ReadinessConfig Readiness { get; set; } = new();
    public GatingConfig Gating { get; set; } = new();

    public static WorkoutPlanningConfig CreateDefault()
    {
        return new WorkoutPlanningConfig
        {
            Readiness = new ReadinessConfig
            {
                SleepLastNight = Clone(DefaultSleepLastNightBands),
                Sleep3dAvg = Clone(DefaultSleep3dAvgBands),
                PhasePrior = new Dictionary<string, int>(DefaultPhasePriors)
            },
            Gating = new GatingConfig
            {
                HiitMenstrualMinEnergy = GatingHiitMenstrualMinimumEnergy
            }
        };
    }

    private static double[][] Clone(double[][] bands)
    {
        return bands.Select(band => band.ToArray()).ToArray();
    }
}

public sealed class ReadinessConfig
{
    private const int DefaultEnergyMultiplier = 5;
    private const int DefaultHighTier = 75;
    private const int DefaultModerateTier = 50;
    private static readonly int[] DefaultPainScores = [15, 10, 3, 0];

    public double[][] SleepLastNight { get; set; } = [];
    public double[][] Sleep3dAvg { get; set; } = [];
    public StepsDeltaConfig StepsDeltaPct { get; set; } = new();
    public int EnergyMultiplier { get; set; } = DefaultEnergyMultiplier;
    public int[] Pain { get; set; } = [.. DefaultPainScores];
    public Dictionary<string, int> PhasePrior { get; set; } = new();
    public int HighTier { get; set; } = DefaultHighTier;
    public int ModerateTier { get; set; } = DefaultModerateTier;
}

public sealed class StepsDeltaConfig
{
    private const int DefaultNormalScore = 15;
    private const int DefaultSpikeScore = 5;
    private const int DefaultDropScore = 10;
    private const int DefaultSpikeThresholdPct = 40;
    private const int DefaultDropThresholdPct = -40;

    public int Normal { get; set; } = DefaultNormalScore;
    public int Spike { get; set; } = DefaultSpikeScore;
    public int Drop { get; set; } = DefaultDropScore;
    public int SpikeThreshold { get; set; } = DefaultSpikeThresholdPct;
    public int DropThreshold { get; set; } = DefaultDropThresholdPct;
}

public sealed class GatingConfig
{
    private const double DefaultHiitMinimumSleepHours = 6.5;
    private const int DefaultHiitMaximumPain = 1;
    private const int DefaultHiitMenstrualMinimumEnergy = 4;
    private const double DefaultLowReadinessSetMultiplier = 0.7;
    private const int DefaultLowReadinessRpeCap = 7;
    private const int DefaultConsecutiveLowDaysToRest = 2;

    public double HiitMinSleep { get; set; } = DefaultHiitMinimumSleepHours;
    public int HiitMaxPain { get; set; } = DefaultHiitMaximumPain;
    public int HiitMenstrualMinEnergy { get; set; } = DefaultHiitMenstrualMinimumEnergy;
    public double LowReadinessSetMultiplier { get; set; } = DefaultLowReadinessSetMultiplier;
    public int LowReadinessRpeCap { get; set; } = DefaultLowReadinessRpeCap;
    public int ConsecutiveLowDaysToRest { get; set; } = DefaultConsecutiveLowDaysToRest;
}
