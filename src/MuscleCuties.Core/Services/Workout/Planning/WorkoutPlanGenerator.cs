using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWorkoutPlanGenerator
{
    bool ShouldRegenerate(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        CyclePhase phase);

    Task<WorkoutPlan> GenerateAsync(
        UserProfile profile,
        CyclePhase phase,
        DateTime createdAt);
}

public sealed class WorkoutPlanGenerator : IWorkoutPlanGenerator
{
    private const string PlanNamePrefix = "Personalized";

    private readonly AppDatabase _database;
    private readonly IReadinessRepository _dailyRepository;
    private readonly IWorkoutInjuryRepository _injuryRepository;
    private readonly IExercisePickerService _exercisePicker;
    private readonly IGatingEngine _gating;
    private readonly IReadinessEngine _readiness;
    private readonly IWeekPlanGenerator _weekGenerator;

    public WorkoutPlanGenerator(
        AppDatabase database,
        IWeekPlanGenerator weekGenerator,
        IExercisePickerService exercisePicker,
        IWorkoutInjuryRepository injuryRepository,
        IReadinessRepository dailyRepository,
        IReadinessEngine readiness,
        IGatingEngine gating)
    {
        _database = database;
        _weekGenerator = weekGenerator;
        _exercisePicker = exercisePicker;
        _injuryRepository = injuryRepository;
        _dailyRepository = dailyRepository;
        _readiness = readiness;
        _gating = gating;
    }

    public bool ShouldRegenerate(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        CyclePhase phase)
    {
        if (activePlan is null)
            return true;

        if (!IsGenerated(activePlan))
            return false;

        var expectedActiveDays = Math.Clamp(profile.WorkoutDaysPerWeek <= 0 ? 3 : profile.WorkoutDaysPerWeek, 2, 6);
        var actualActiveDays = activePlanDays
            .Where(day => day.WorkoutType != WorkoutType.Rest)
            .Select(day => day.DayOfWeek)
            .Distinct()
            .Count();
        return activePlan.CyclePhaseTarget != phase ||
               !activePlan.Name.Equals(BuildPlanName(phase), StringComparison.Ordinal) ||
               activePlanDays.Select(day => day.DayOfWeek).Distinct().Count() != 7 ||
               actualActiveDays != expectedActiveDays ||
               activePlanDays.Any(day => day.WorkoutType != WorkoutType.Rest && day.WorkoutDayExercises.Count == 0);
    }

    public async Task<WorkoutPlan> GenerateAsync(
        UserProfile profile,
        CyclePhase phase,
        DateTime createdAt)
    {
        await _database.SeedWorkoutPlanningDataAsync().ConfigureAwait(false);
        var exerciseLibrary = await _database.Exercises.AsNoTracking().ToListAsync().ConfigureAwait(false);

        var injuries = await _injuryRepository.GetActiveAsync(profile.UserId).ConfigureAwait(false);
        var adaptiveProfile = AdaptiveProfileMapper.FromUserProfile(profile, injuries);
        var week = await _weekGenerator.GenerateWeekAsync(new WeekGenerationInput
        {
            DaysPerWeek = adaptiveProfile.DaysPerWeek,
            Experience = adaptiveProfile.Experience,
            Goal = adaptiveProfile.Goal,
            SessionMinutesTarget = adaptiveProfile.SessionMinutesCap,
            SelectedActivities = adaptiveProfile.Selected,
            HasRunningOrClimbing = adaptiveProfile.Selected.Contains(WorkoutActivityType.Running) ||
                                   adaptiveProfile.Selected.Contains(WorkoutActivityType.RockClimbing),
            Phase = phase
        }).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var neutralInputs = BuildPlanningInputs(today, adaptiveProfile);
        var currentInputs = await _dailyRepository.GetInputsForAsync(profile.UserId, today).ConfigureAwait(false) ??
                            neutralInputs;
        var consecutiveLowDays = await _dailyRepository
            .GetConsecutiveLowDaysAsync(profile.UserId, today)
            .ConfigureAwait(false);
        var equipment = ExercisePickerService.MapEquipment(adaptiveProfile.Equipment);
        var injuryFlags = WorkoutInjuryRules.ToFlags(adaptiveProfile.Injuries);
        var blockedActivities = WorkoutInjuryRules.GetBlockedActivities(adaptiveProfile.Injuries);
        var exerciseLookup = BuildExerciseLookup(exerciseLibrary);

        var plan = new WorkoutPlan
        {
            UserId = profile.UserId,
            Name = BuildPlanName(phase),
            IsActive = true,
            CyclePhaseTarget = phase,
            CreatedAt = createdAt
        };

        foreach (var session in week.Sessions
                     .OrderBy(session => ToDayIndex(session.Day))
                     .ThenBy(session => session.IsRecovery ? 2 : session.IsCardio ? 1 : 0))
        {
            var isToday = session.Day == today.DayOfWeek;
            var inputs = isToday ? currentInputs : neutralInputs;

            if (session.IsRecovery)
            {
                plan.WorkoutDays.Add(BuildRecoveryDay(session, adaptiveProfile, exerciseLookup));
                continue;
            }

            if (session.IsCardio && session.CardioActivity is not null)
            {
                var activity = session.CardioActivity.Value;
                if (isToday)
                {
                    var readiness = _readiness.Compute(inputs, phase, adaptiveProfile.Baselines);
                    activity = _gating.Resolve(
                        WorkoutType.Cardio,
                        adaptiveProfile,
                        inputs,
                        readiness,
                        phase,
                        consecutiveLowDays).Activity;
                }

                if (blockedActivities.Contains(activity))
                    activity = WorkoutActivityType.Yoga;

                var day = activity == WorkoutActivityType.Yoga
                    ? BuildRecoveryDay(session, adaptiveProfile, exerciseLookup)
                    : BuildConditioningDay(session, activity, phase, exerciseLookup);
                plan.WorkoutDays.Add(day);
                continue;
            }

            var strengthReadiness = _readiness.Compute(inputs, phase, adaptiveProfile.Baselines);
            var strengthGating = _gating.Resolve(
                WorkoutType.Strength,
                adaptiveProfile,
                inputs,
                strengthReadiness,
                phase,
                isToday ? consecutiveLowDays : 0);
            var strengthDay = await BuildStrengthDayAsync(
                session,
                strengthGating,
                equipment,
                injuryFlags,
                exerciseLookup,
                profile.UserId).ConfigureAwait(false);
            plan.WorkoutDays.Add(strengthDay);
        }

        var activeDays = plan.WorkoutDays.Select(day => day.DayOfWeek).ToHashSet();
        foreach (var dayOfWeek in Enumerable.Range(0, 7).Where(day => !activeDays.Contains(day)))
        {
            plan.WorkoutDays.Add(new WorkoutDay
            {
                DayOfWeek = dayOfWeek,
                WorkoutType = WorkoutType.Rest,
                Name = "Living happy life"
            });
        }

        return plan;
    }

    private async Task<WorkoutDay> BuildStrengthDayAsync(
        PlannedSession session,
        GatingResult gating,
        EquipmentSet equipment,
        InjuryFlag injuries,
        IReadOnlyDictionary<string, Exercise> exerciseLookup,
        int userId = 0)
    {
        if (gating.SetMultiplier <= 0)
        {
            return new WorkoutDay
            {
                DayOfWeek = ToDayIndex(session.Day),
                WorkoutType = WorkoutType.Rest,
                Name = "Living happy life"
            };
        }

        var prescribed = await _exercisePicker.PickForSessionAsync(
            session,
            equipment,
            injuries,
            gating.SetMultiplier,
            gating.RpeCap,
            userId);
        var day = new WorkoutDay
        {
            DayOfWeek = ToDayIndex(session.Day),
            WorkoutType = WorkoutType.Strength,
            Name = BuildStrengthName(session.ArchetypeCode, gating.Activity)
        };

        foreach (var picked in prescribed.Exercises)
        {
            var exercise = ResolveExercise(exerciseLookup, picked.ExerciseId, picked.ExerciseName);
            day.WorkoutDayExercises.Add(new WorkoutDayExercise
            {
                ExerciseId = exercise.Id,
                Sets = picked.Sets,
                Reps = picked.RepsMax
            });
        }

        return day;
    }

    private WorkoutDay BuildConditioningDay(
        PlannedSession planned,
        WorkoutActivityType activity,
        CyclePhase phase,
        IReadOnlyDictionary<string, Exercise> exerciseLookup)
    {
        var exerciseName = ResolveActivityExerciseName(activity, phase);
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);

        return new WorkoutDay
        {
            DayOfWeek = ToDayIndex(planned.Day),
            WorkoutType = activity == WorkoutActivityType.RockClimbing
                ? WorkoutType.Strength
                : WorkoutType.Cardio,
            Name = BuildActivityName(activity),
            WorkoutDayExercises =
            [
                new WorkoutDayExercise
                {
                    ExerciseId = exercise.Id,
                    DurationSeconds = GetActivityDurationMinutes(activity) * 60
                }
            ]
        };
    }

    private WorkoutDay BuildRecoveryDay(
        PlannedSession planned,
        AdaptiveProfile profile,
        IReadOnlyDictionary<string, Exercise> exerciseLookup)
    {
        var exerciseName = profile.Selected.Contains(WorkoutActivityType.Yoga) ? "Yoga Flow" : "Mobility Flow";
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);

        return new WorkoutDay
        {
            DayOfWeek = ToDayIndex(planned.Day),
            WorkoutType = WorkoutType.Recovery,
            Name = "Low intensity recovery training",
            WorkoutDayExercises =
            [
                new WorkoutDayExercise
                {
                    ExerciseId = exercise.Id,
                    DurationSeconds = 38 * 60
                }
            ]
        };
    }

    private static IReadOnlyDictionary<string, Exercise> BuildExerciseLookup(
        IReadOnlyCollection<Exercise> exercises)
    {
        var lookup = new Dictionary<string, Exercise>(StringComparer.OrdinalIgnoreCase);
        foreach (var exercise in exercises)
        {
            lookup.TryAdd(exercise.Code, exercise);
            lookup.TryAdd(Normalize(exercise.Name), exercise);
        }

        return lookup;
    }

    private static Exercise ResolveExercise(
        IReadOnlyDictionary<string, Exercise> lookup,
        int? planningExerciseId,
        string name)
    {
        if (planningExerciseId is not null &&
            lookup.TryGetValue($"ENGINE_{planningExerciseId}", out var byPlanningExerciseId))
            return byPlanningExerciseId;

        if (lookup.TryGetValue(Normalize(name), out var byName))
            return byName;

        throw new InvalidOperationException($"Workout exercise '{name}' is missing from the exercise catalog.");
    }

    private static DailyInputs BuildPlanningInputs(DateOnly date, AdaptiveProfile profile)
    {
        return new DailyInputs(date, 7.5, 7.5, profile.BaselineSteps, profile.BaselineSteps, 3, 0, false, null);
    }

    private static string BuildPlanName(CyclePhase phase) => $"Personalized {phase.ToString().ToLowerInvariant()} training";

    private static string BuildStrengthName(string archetypeCode, WorkoutActivityType activity)
    {
        var prefix = activity == WorkoutActivityType.StrengthHighIntensity ? "Heavy" : "Progressive";
        return archetypeCode switch
        {
            "P" => $"{prefix} glute and pull strength",
            "S" => $"{prefix} leg strength",
            "U" => $"{prefix} upper body strength",
            "U2" => $"{prefix} upper body and glute strength",
            "G" => "Glute accessory training",
            "F" => $"{prefix} full body strength",
            _ => $"{prefix} strength training"
        };
    }

    private static string BuildActivityName(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.RockClimbing => "Climbing strength session",
        WorkoutActivityType.Hiit => "HIIT conditioning",
        WorkoutActivityType.Cycling => "Cycling conditioning",
        WorkoutActivityType.Running => "Running conditioning",
        WorkoutActivityType.Swimming => "Swimming conditioning",
        _ => "Conditioning session"
    };

    private static string ResolveActivityExerciseName(WorkoutActivityType activity, CyclePhase phase) => activity switch
    {
        WorkoutActivityType.RockClimbing => "Rock Climbing",
        WorkoutActivityType.Hiit => "HIIT Intervals",
        WorkoutActivityType.Cycling when phase == CyclePhase.Ovulatory => "Cycling Intervals",
        WorkoutActivityType.Cycling => "Zone 2 Ride",
        WorkoutActivityType.Running when phase == CyclePhase.Menstrual => "Easy Run",
        WorkoutActivityType.Running when phase == CyclePhase.Ovulatory => "Running Intervals",
        WorkoutActivityType.Running => "Tempo Run",
        WorkoutActivityType.Swimming => "Swimming",
        _ => "Mobility Flow"
    };

    private static int GetActivityDurationMinutes(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.RockClimbing => 75,
        WorkoutActivityType.Running => 38,
        WorkoutActivityType.Cycling => 38,
        WorkoutActivityType.Swimming => 30,
        WorkoutActivityType.Hiit => 20,
        _ => 30
    };

    private static bool IsGenerated(WorkoutPlan plan)
    {
        return plan.CyclePhaseTarget.HasValue ||
               plan.Name.StartsWith(PlanNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Generated training", StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Glow Plan", StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Cycle Plan", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    private static int ToDayIndex(DayOfWeek day) => (int)day;
}
