using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    // ---- Thresholds: named once, referenced everywhere ----
    private const int HighPainThreshold = 4;      // pain >= this => body needs recovery
    private const int LowPainThreshold = 2;        // pain <= this => body can handle peak load
    private const int ModeratePainThreshold = 3;   // pain <= this => follicular ramp is safe
    private const int LowEnergyThreshold = 2;       // energy in (0, this] => fatigue-limited
    private const int HighEnergyThreshold = 4;      // energy >= this => ready for peak load
    private const int MinTrainingDaysPerWeek = 1;
    private const int MaxTrainingDaysPerWeek = 6;
    private const string RestDayName = "Rest and recovery";

    /// <summary>
    /// Represents where the user's body sits today on the training-readiness spectrum,
    /// derived once from cycle phase + symptom baseline, and reused for naming, day count,
    /// and set/rep prescription so all three always stay in sync.
    /// </summary>
    private enum TrainingIntensityTier
    {
        Recovery,   // menstrual or severe pain/fatigue: lowest volume, gentlest movements
        Deload,     // luteal phase without severe symptoms: reduced but not minimal load
        Standard,   // default training capacity
        Building,   // follicular phase with low pain: safe to ramp intensity/duration
        Peak        // ovulatory phase with good recovery markers: highest capacity window
    }

    public bool ShouldReplaceGeneratedPlan(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase)
    {
        if (activePlan is null)
            return true;

        if (!IsGeneratedPlan(activePlan))
            return false;

        var expectedTrainingDays = CalculateTrainingDays(profile, snapshot, phase);

        return activePlan.CyclePhaseTarget != phase ||
               activePlan.Name != BuildGeneratedPlanName(profile, snapshot, phase) ||
               activePlanDays.Count != 7 ||
               activePlanDays.Count(day => day.WorkoutType != WorkoutType.Rest) != expectedTrainingDays ||
               activePlanDays.Any(HasDeprecatedGeneratedWorkoutShape) ||
               activePlanDays.Any(IsUnderbuiltGeneratedDay);
    }

    public WorkoutPlan BuildGeneratedPlan(
        int userId,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        IReadOnlyCollection<Exercise> exerciseLibrary,
        CyclePhase phase,
        DateTime createdAt)
    {
        var trainingDays = CalculateTrainingDays(profile, snapshot, phase);
        var schedule = BuildSchedule(trainingDays);
        var prescription = BuildPrescription(profile, snapshot, phase);
        var templates = BuildSessionTemplates(profile, snapshot, phase, trainingDays);
        var exercisesByName = exerciseLibrary.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var plan = new WorkoutPlan
        {
            UserId = userId,
            Name = BuildGeneratedPlanName(profile, snapshot, phase),
            IsActive = true,
            CyclePhaseTarget = phase,
            CreatedAt = createdAt
        };

        var templateByDay = templates
            .Select((template, index) => new { DayOfWeek = schedule[index], Template = template })
            .ToDictionary(item => item.DayOfWeek, item => item.Template);

        for (var dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++)
        {
            if (!templateByDay.TryGetValue(dayOfWeek, out var template))
            {
                plan.WorkoutDays.Add(new WorkoutDay
                {
                    DayOfWeek = dayOfWeek,
                    WorkoutType = WorkoutType.Rest,
                    Name = RestDayName
                });
                continue;
            }

            plan.WorkoutDays.Add(BuildWorkoutDay(dayOfWeek, template, prescription, exercisesByName, phase));
        }

        return plan;
    }

    private static WorkoutDay BuildWorkoutDay(
        int dayOfWeek,
        SessionTemplate template,
        WorkoutPrescription prescription,
        IReadOnlyDictionary<string, Exercise> exercisesByName,
        CyclePhase phase)
    {
        var day = new WorkoutDay
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = BuildWorkoutType(template),
            Name = template.Name
        };

        foreach (var exerciseName in template.ExerciseNames)
        {
            if (!exercisesByName.TryGetValue(exerciseName, out var exercise))
                continue;

            var isTimed = IsTimedExercise(template, exerciseName);

            day.WorkoutDayExercises.Add(new WorkoutDayExercise
            {
                ExerciseId = exercise.Id,
                Sets = isTimed ? 0 : prescription.Sets,
                Reps = isTimed ? 0 : prescription.Reps,
                DurationSeconds = isTimed
                    ? BuildTimedExerciseSeconds(exerciseName, prescription.TimedExerciseSeconds, phase)
                    : null
            });
        }

        return day;
    }

    /// <summary>
    /// Single source of truth for "how ready is the body today". Every other
    /// method (naming, day count, prescription) reads from this instead of
    /// recomputing its own version of the same condition.
    /// </summary>
    private static TrainingIntensityTier DetermineIntensityTier(CyclePhase phase, PhaseBaseline baseline)
    {
        var hasSevereSymptoms = baseline.Pain >= HighPainThreshold ||
                                 baseline.Energy is > 0 and <= LowEnergyThreshold;

        if (phase is CyclePhase.Menstrual || hasSevereSymptoms)
            return TrainingIntensityTier.Recovery;

        if (phase is CyclePhase.Luteal)
            return TrainingIntensityTier.Deload;

        if (phase is CyclePhase.Ovulatory &&
            baseline.Pain <= LowPainThreshold &&
            (baseline.Energy == 0 || baseline.Energy >= HighEnergyThreshold))
            return TrainingIntensityTier.Peak;

        if (phase is CyclePhase.Follicular && baseline.Pain <= ModeratePainThreshold)
            return TrainingIntensityTier.Building;

        return TrainingIntensityTier.Standard;
    }

    private static bool IsReducedCapacity(TrainingIntensityTier tier) =>
        tier is TrainingIntensityTier.Recovery or TrainingIntensityTier.Deload;

    private static bool IsGeneratedPlan(WorkoutPlan plan) =>
        plan.Name.StartsWith(GeneratedPlanPrefix, StringComparison.OrdinalIgnoreCase) ||
        plan.Name.StartsWith(PreviousGeneratedPlanPrefix, StringComparison.OrdinalIgnoreCase) ||
        plan.Name.StartsWith(LegacyGeneratedPlanPrefix, StringComparison.OrdinalIgnoreCase) ||
        GeneratedPlanNames.Contains(plan.Name, StringComparer.OrdinalIgnoreCase);

    private static bool IsUnderbuiltGeneratedDay(WorkoutDay day)
    {
        if (day.WorkoutType is WorkoutType.Rest)
            return false;

        if (day.WorkoutDayExercises.Count == 0)
            return true;

        const int minStrengthExercises = 5;
        const int minCardioSeconds = 1_200;
        const int minRecoverySeconds = 2_400;

        return day.WorkoutType switch
        {
            WorkoutType.Strength => day.WorkoutDayExercises.Count < minStrengthExercises,
            WorkoutType.Cardio => day.WorkoutDayExercises.Sum(e => e.DurationSeconds ?? 0) < minCardioSeconds,
            WorkoutType.Recovery => day.WorkoutDayExercises.Sum(e => e.DurationSeconds ?? 0) < minRecoverySeconds,
            _ => false
        };
    }

    private static readonly string[] DeprecatedWorkoutNames =
    [
        "Conditioning intervals", "Steady aerobic ride", "Yoga mobility flow",
        "Power yoga strength", "Pilates", "Mobility Reset", "Easy walk", "Dance Cardio"
    ];

    private static bool HasDeprecatedGeneratedWorkoutShape(WorkoutDay day) =>
        ContainsAny(day.Name, DeprecatedWorkoutNames);

    private static readonly string[] RecoveryKeywords =
        ["Recovery", "Mobility", "Yoga", "Pilates", "Reset"];

    private static WorkoutType BuildWorkoutType(SessionTemplate template)
    {
        if (template.WorkoutTypeOverride is not null)
            return template.WorkoutTypeOverride.Value;

        var isRecoveryFocused = ContainsAny(template.Name, RecoveryKeywords) ||
            template.ExerciseNames.Any(name => ContainsAny(name, RecoveryKeywords));

        if (isRecoveryFocused)
            return WorkoutType.Recovery;

        return template.IsTimed ? WorkoutType.Cardio : WorkoutType.Strength;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the plan title. Deliberately reads from the same
    /// <see cref="DetermineIntensityTier"/> result used for day count and
    /// prescription, so the name always reflects what the plan actually does.
    /// </summary>
    private static string BuildGeneratedPlanName(UserProfile profile, UserProfileSnapshot? snapshot, CyclePhase phase)
    {
        var baseline = ReadPhaseBaseline(snapshot, phase);
        var tier = DetermineIntensityTier(phase, baseline);

        if (tier is TrainingIntensityTier.Recovery)
            return "Low intensity recovery training";

        if (profile.TrainingExperienceLevel is TrainingExperienceLevel.Advanced)
        {
            return tier switch
            {
                TrainingIntensityTier.Peak => "Peak physique strength",
                TrainingIntensityTier.Deload => "Controlled physique training",
                _ => "Advanced physique strength"
            };
        }

        return (profile.Goal, tier) switch
        {
            (UserGoal.Strength, TrainingIntensityTier.Peak) => "Peak strength training",
            (UserGoal.Strength, TrainingIntensityTier.Deload) => "Controlled strength training",
            (UserGoal.Strength, _) => "Progressive strength training",
            (UserGoal.FatLoss, TrainingIntensityTier.Peak) => "Interval conditioning training",
            (UserGoal.FatLoss, TrainingIntensityTier.Deload) => "Low impact conditioning training",
            (UserGoal.FatLoss, _) => "Conditioning and strength training",
            (UserGoal.MuscleTone, TrainingIntensityTier.Peak) => "Heavy full body training",
            (UserGoal.MuscleTone, TrainingIntensityTier.Deload) => "Controlled full body training",
            (UserGoal.MuscleTone, _) => "Full body hypertrophy training",
            (UserGoal.MaintainHealth, TrainingIntensityTier.Deload) => "Balanced recovery training",
            _ => "Balanced strength and cardio training"
        };
    }

    private static int CalculateTrainingDays(UserProfile profile, UserProfileSnapshot? snapshot, CyclePhase phase)
    {
        var requestedDays = profile.WorkoutDaysPerWeek <= 0
            ? 3
            : Math.Clamp(profile.WorkoutDaysPerWeek, MinTrainingDaysPerWeek, MaxTrainingDaysPerWeek);

        var baseline = ReadPhaseBaseline(snapshot, phase);
        var tier = DetermineIntensityTier(phase, baseline);

        if (IsReducedCapacity(tier) && requestedDays > 2)
            requestedDays -= 1;

        if (!IsReducedCapacity(tier) && profile.Goal is UserGoal.Strength && requestedDays == 1)
            requestedDays = 2;

        return requestedDays;
    }

    private static WorkoutPrescription BuildPrescription(UserProfile profile, UserProfileSnapshot? snapshot, CyclePhase phase)
    {
        var baseline = ReadPhaseBaseline(snapshot, phase);
        var tier = DetermineIntensityTier(phase, baseline);

        var sets = (profile.Goal, profile.TrainingExperienceLevel) switch
        {
            (UserGoal.Strength, TrainingExperienceLevel.Advanced) => 5,
            (UserGoal.Strength, TrainingExperienceLevel.Intermediate) => 4,
            (UserGoal.Strength, _) => 3,
            (_, TrainingExperienceLevel.Advanced) => 4,
            (_, TrainingExperienceLevel.Intermediate) => 4,
            _ => 3
        };

        var reps = profile.Goal switch
        {
            UserGoal.Strength => profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner ? 6 : 5,
            UserGoal.FatLoss => 14,
            UserGoal.MuscleTone => 12,
            _ => 10
        };

        var timedSeconds = profile.Goal switch
        {
            UserGoal.FatLoss => 1_200,
            UserGoal.MuscleTone => 900,
            UserGoal.Strength => 720,
            _ => 900
        };

        // Follicular ramp: rising energy phase, safe to extend cardio/conditioning duration.
        if (tier is TrainingIntensityTier.Building)
            timedSeconds += 120;

        // Ovulatory peak: highest hormonal readiness window, push volume and duration.
        if (tier is TrainingIntensityTier.Peak)
        {
            sets += profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner ? 0 : 1;
            timedSeconds += 180;
        }

        // Recovery / deload: pull back load proportionally to protect consistency, not just this week.
        if (IsReducedCapacity(tier))
        {
            var reduction = tier is TrainingIntensityTier.Recovery ? 2 : 1;
            sets = Math.Max(2, sets - reduction);
            reps = profile.Goal is UserGoal.Strength ? 8 : Math.Max(8, reps - 2);
            timedSeconds = Math.Max(600, timedSeconds - (reduction * 90));
        }

        var strengthStyle = WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes);
        var canUseExpressStrength =
            strengthStyle is StrengthTrainingStyle.ExpressHard &&
            profile.Goal is UserGoal.Strength &&
            !IsReducedCapacity(tier) &&
            (phase is not CyclePhase.Menstrual || baseline.Energy >= HighEnergyThreshold);

        if (canUseExpressStrength)
        {
            sets = profile.TrainingExperienceLevel is TrainingExperienceLevel.Advanced ? 5 : 4;
            reps = profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner ? 6 : 4;
        }

        return new WorkoutPrescription(sets, reps, timedSeconds);
    }

    // Keyword -> phase-aware duration mapping, evaluated top-down (first match wins).
    // Replaces a 15-branch if-chain with a data table that's easy to scan and extend.
    private static readonly (string[] Keywords, Func<CyclePhase, int> DurationSeconds)[] TimedExerciseDurations =
    [
        (["Vinyasa Flow"], phase => phase is CyclePhase.Menstrual ? 1_500 : 1_800),
        (["Slow Flow Yoga"], phase => phase is CyclePhase.Menstrual ? 1_500 : 1_800),
        (["Yoga Flow"], phase => phase is CyclePhase.Menstrual ? 1_500 : 1_800),
        (["Hip Opening Yoga"], phase => phase is CyclePhase.Menstrual ? 900 : 1_200),
        (["Power Yoga", "Pilates Flow"], phase => phase is CyclePhase.Menstrual ? 1_500 : 2_100),
        (["Yin Yoga", "Restorative Yoga"], phase => phase is CyclePhase.Ovulatory ? 2_100 : 2_400),
        (["Active Recovery Flow"], phase => phase is CyclePhase.Menstrual ? 1_500 : 1_800),
        (["Mobility Flow"], _ => 720),
        (["Breathing Reset"], phase => phase is CyclePhase.Ovulatory ? 360 : 300),
        (["Easy Walk", "Easy Run"], phase => phase is CyclePhase.Menstrual ? 1_200 : 1_800),
        (["Zone 2 Ride", "Swimming"], phase => phase switch
        {
            CyclePhase.Menstrual => 1_800,
            CyclePhase.Ovulatory => 3_000,
            _ => 2_400
        }),
        (["Cycling Intervals"], phase => phase switch
        {
            CyclePhase.Menstrual => 1_500,
            CyclePhase.Ovulatory => 3_000,
            _ => 2_400
        }),
        (["Running Intervals", "Tempo Run"], phase => phase switch
        {
            CyclePhase.Menstrual => 1_500,
            CyclePhase.Ovulatory => 2_700,
            _ => 2_100
        }),
        (["HIIT Intervals", "Bike Intervals", "Dance Cardio"], phase => phase switch
        {
            CyclePhase.Menstrual => 1_200,
            CyclePhase.Ovulatory => 2_100,
            _ => 1_800
        }),
        (["Rock Climbing", "Climb"], phase => phase switch
        {
            CyclePhase.Menstrual => 1_800,
            CyclePhase.Luteal => 2_400,
            CyclePhase.Ovulatory => 3_600,
            _ => 3_000
        })
    ];

    private static int BuildTimedExerciseSeconds(string exerciseName, int defaultSeconds, CyclePhase phase)
    {
        foreach (var (keywords, durationSeconds) in TimedExerciseDurations)
        {
            if (ContainsAny(exerciseName, keywords))
                return durationSeconds(phase);
        }

        return phase switch
        {
            CyclePhase.Menstrual => 600,
            CyclePhase.Luteal => 720,
            _ => defaultSeconds
        };
    }

    private static bool IsTimedExercise(SessionTemplate template, string exerciseName) =>
        template.IsTimed || template.TimedExerciseNames.Contains(exerciseName, StringComparer.OrdinalIgnoreCase);

    private static int[] BuildSchedule(int trainingDays) =>
        trainingDays switch
        {
            <= 1 => [1],
            2 => [1, 4],
            3 => [1, 3, 5],
            4 => [1, 2, 4, 6],
            5 => [1, 2, 4, 5, 6],
            _ => [1, 2, 3, 4, 5, 6]
        };

    private sealed record WorkoutPrescription(int Sets, int Reps, int TimedExerciseSeconds);
}
