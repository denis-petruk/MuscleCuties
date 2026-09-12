using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Workout.Planning;

public sealed record AdaptiveProfile(
    int UserId,
    UserGoal Goal,
    TrainingExperienceLevel Experience,
    int DaysPerWeek,
    int SessionMinutesCap,
    HashSet<WorkoutActivityType> Selected,
    StrengthTrainingStyle Style,
    Equipment Equipment,
    List<Injury> Injuries,
    int BaselineSteps,
    CyclePhaseBaselines? Baselines = null);

public sealed record Injury(InjurySite Site, InjuryStatus Status, DateOnly Since);

public sealed record DailyInputs(
    DateOnly Date,
    double SleepHours,
    double Sleep3dAvg,
    int StepsYesterday,
    int Steps7dAvg,
    int Energy,
    int Pain,
    bool Bloating,
    double? WeightKg);

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

    public PhaseBaseline ForPhase(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => Menstrual,
        CyclePhase.Follicular => Follicular,
        CyclePhase.Ovulatory => Ovulatory,
        CyclePhase.Luteal => Luteal,
        _ => Follicular
    };
}

public sealed record PhaseBaseline(int Pain, int Energy);

public sealed record GatingResult(
    WorkoutActivityType Activity,
    StrengthTrainingStyle Style,
    double SetMultiplier,
    int RpeCap,
    List<string> Notes);
