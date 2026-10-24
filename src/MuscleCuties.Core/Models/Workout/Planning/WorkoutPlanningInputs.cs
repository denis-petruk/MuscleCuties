using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Workout.Planning;

public sealed record AdaptiveProfile(
    int UserId,
    UserGoal Goal,
    TrainingExperienceLevel Experience,
    int DaysPerWeek,
    int SessionMinutesCap,
    HashSet<WorkoutActivityType> Selected,
    Equipment Equipment,
    List<Injury> Injuries,
    int BaselineSteps,
    CyclePhaseBaselines? Baselines = null);

public sealed record Injury(InjuryFlag Site, InjuryStatus Status, DateOnly Since);

public sealed record DailyInputs(
    DateOnly Date,
    double SleepHours,
    double Sleep3dAvg,
    int StepsYesterday,
    int Steps7dAvg,
    int Energy,
    int Pain,
    bool Bloating,
    double? WeightKg,
    int? RestingHeartRate = null,
    double? AverageActiveEnergyKcal = null,
    SleepQuality? SleepQuality = null,
    SorenessLevel? Soreness = null,
    WorkoutLoad? PreviousDayLoad = null)
{
    public static DailyInputs CreateSafe(
        DateOnly date,
        double sleepHours,
        double sleep3dAvg,
        int stepsYesterday,
        int steps7dAvg,
        int energy,
        int pain,
        bool bloating,
        double? weightKg,
        int? restingHeartRate = null,
        double? averageActiveEnergyKcal = null,
        SleepQuality? sleepQuality = null,
        SorenessLevel? soreness = null,
        WorkoutLoad? previousDayLoad = null)
    {
        var safeSleepHours = double.IsFinite(sleepHours) && sleepHours >= 0 ? Math.Clamp(sleepHours, 0, 24) : 0;
        var safeSleep3dAvg = double.IsFinite(sleep3dAvg) && sleep3dAvg >= 0 ? Math.Clamp(sleep3dAvg, 0, 24) : 0;
        var safeStepsYesterday = stepsYesterday >= 0 ? Math.Clamp(stepsYesterday, 0, 200_000) : 0;
        var safeSteps7dAvg = steps7dAvg >= 0 ? Math.Clamp(steps7dAvg, 0, 200_000) : 0;
        var safeEnergy = energy is >= 1 and <= 5 ? energy : 1;
        var safePain = pain is >= 0 and <= 3 ? pain : 3;
        var safeWeight = weightKg is > 0 and <= 500 && double.IsFinite(weightKg.Value) ? weightKg : null;
        var safeRhr = restingHeartRate is >= 30 and <= 250 ? restingHeartRate : null;
        var safeActiveEnergy = averageActiveEnergyKcal is >= 0 and <= 15_000 &&
                               double.IsFinite(averageActiveEnergyKcal.Value)
            ? averageActiveEnergyKcal
            : null;

        var safeSleepQuality = sleepQuality is not null &&
                               sleepQuality.Value >= Enums.Workout.SleepQuality.VeryPoor &&
                               sleepQuality.Value <= Enums.Workout.SleepQuality.VeryGood
            ? sleepQuality
            : null;

        var safeSoreness = soreness is not null &&
                           soreness.Value >= SorenessLevel.None &&
                           soreness.Value <= SorenessLevel.Severe
            ? soreness
            : null;
        var safeLoad = previousDayLoad switch
        {
            null => null,
            _ when previousDayLoad.LoadScalar.HasValue && (!double.IsFinite(previousDayLoad.LoadScalar.Value) ||
                                                           previousDayLoad.LoadScalar.Value < 0) =>
                new WorkoutLoad(previousDayLoad.Kind, 0, previousDayLoad.SetsCompleted,
                    Math.Max(0, previousDayLoad.TotalVolumeKg), Math.Max(0, previousDayLoad.TotalDurationSeconds)),
            _ => previousDayLoad
        };

        return new DailyInputs(
            date,
            safeSleepHours,
            safeSleep3dAvg,
            safeStepsYesterday,
            safeSteps7dAvg,
            safeEnergy,
            safePain,
            bloating,
            safeWeight,
            safeRhr,
            safeActiveEnergy,
            safeSleepQuality,
            safeSoreness,
            safeLoad);
    }
}

public sealed record Readiness(
    int Score,
    ReadinessTier Tier,
    IReadOnlyDictionary<string, int> Breakdown);

public sealed record CyclePhaseBaselines(
    PhaseBaseline Menstrual,
    PhaseBaseline Follicular,
    PhaseBaseline Ovulatory,
    PhaseBaseline Luteal)
{
    public static CyclePhaseBaselines Default => new(
        new PhaseBaseline(3, 2),
        new PhaseBaseline(1, 4),
        new PhaseBaseline(2, 5),
        new PhaseBaseline(3, 3));

    public PhaseBaseline ForPhase(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Menstrual,
            CyclePhase.Follicular => Follicular,
            CyclePhase.Ovulatory => Ovulatory,
            CyclePhase.Luteal => Luteal,
            _ => Follicular
        };
    }
}

public sealed record PhaseBaseline(int Pain, int Energy);

public sealed record GatingResult(
    WorkoutActivityType Activity,
    double SetMultiplier,
    int RpeCap,
    List<string> Notes);
