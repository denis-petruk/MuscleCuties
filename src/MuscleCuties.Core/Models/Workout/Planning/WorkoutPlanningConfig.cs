namespace MuscleCuties.Core.Models.Workout.Planning;

/// <summary>Takes the readiness and gating settings, so every rule reads one configuration.</summary>
public sealed class WorkoutPlanningConfig
{
    /// <summary>The readiness scoring settings.</summary>
    public ReadinessConfig Readiness { get; set; } = new();
    /// <summary>The thresholds that withhold a hard session.</summary>
    public GatingConfig Gating { get; set; } = new();

    /// <summary>The default configuration, which is the only one the app ships.</summary>
    public static WorkoutPlanningConfig CreateDefault()
    {
        return new WorkoutPlanningConfig
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
                    ["Menstrual"] = -10,
                    ["Luteal"] = -5,
                    ["Follicular"] = 0,
                    ["Ovulatory"] = 0,
                    ["Unknown"] = 0
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
}

/// <summary>The weights and bands the readiness score is built from.</summary>
public sealed class ReadinessConfig
{
    /// <summary>The scoring band for last night's sleep.</summary>
    public double[][] SleepLastNight { get; set; } = [];
    /// <summary>The scoring band for the three-day sleep average, which smooths one bad night.</summary>
    public double[][] Sleep3dAvg { get; set; } = [];
    /// <summary>The scoring band for the step change against her baseline.</summary>
    public StepsDeltaConfig StepsDeltaPct { get; set; } = new();
    /// <summary>How strongly her reported energy weighs in the score.</summary>
    public int EnergyMultiplier { get; set; } = 5;
    /// <summary>The scoring band for reported pain, which can only lower the score.</summary>
    public int[] Pain { get; set; } = [15, 10, 3, 0];
    /// <summary>The prior the cycle phase applies, bounded so a phase never lifts the score on its own.</summary>
    public Dictionary<string, int> PhasePrior { get; set; } = new();
    /// <summary>The score above which a session runs at full load.</summary>
    public int HighTier { get; set; } = 75;
    /// <summary>The score above which a session is reduced rather than withheld.</summary>
    public int ModerateTier { get; set; } = 50;
}

/// <summary>The step change bands, which turn a percentage into a score contribution.</summary>
public sealed class StepsDeltaConfig
{
    /// <summary>The contribution when her steps are within the normal band.</summary>
    public int Normal { get; set; } = 15;
    /// <summary>The contribution when her steps spike above the normal band.</summary>
    public int Spike { get; set; } = 5;
    /// <summary>The contribution when her steps fall below the normal band.</summary>
    public int Drop { get; set; } = 10;
    /// <summary>The change above which steps count as a spike.</summary>
    public int SpikeThreshold { get; set; } = 40;
    /// <summary>The change below which steps count as a drop.</summary>
    public int DropThreshold { get; set; } = -40;
}

/// <summary>The thresholds that withhold or reduce a session before it is prescribed.</summary>
public sealed class GatingConfig
{
    /// <summary>The sleep below which HIIT is withheld.</summary>
    public double HiitMinSleep { get; set; } = 6.5;
    /// <summary>The pain above which HIIT is withheld.</summary>
    public int HiitMaxPain { get; set; } = 1;
    /// <summary>The reported energy below which HIIT is withheld during menstruation.</summary>
    public int HiitMenstrualMinEnergy { get; set; } = 4;
    /// <summary>The fraction of sets kept when readiness is low.</summary>
    public double LowReadinessSetMultiplier { get; set; } = 0.7;
    /// <summary>The effort ceiling applied when readiness is low.</summary>
    public int LowReadinessRpeCap { get; set; } = 7;
    /// <summary>How many low days in a row force a rest day instead of a reduced session.</summary>
    public int ConsecutiveLowDaysToRest { get; set; } = 2;
}
