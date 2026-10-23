using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Creates the legacy four-week phase/date sequence and assembles persistence-neutral four-week workout plans.</summary>
public static class FourWeekPlanRule
{
    public const int WeekCount = 4;
    public const string RestDayName = "Living happy life";
    public const string RecoveryDayBaseName = "Low intensity recovery training";
    public const int RecoverySessionDurationSeconds = 38 * SecondsPerMinute;
    private const int DaysPerWeek = 7;
    private const int SecondsPerMinute = 60;
    private const double DefaultSleepHours = 7.5;
    private const int DefaultEnergyLevel = 3;
    private const int DefaultPainLevel = 0;
    private const int RockClimbingDurationMinutes = 75;
    private const int RunningDurationMinutes = 38;
    private const int CyclingDurationMinutes = 38;
    private const int SwimmingDurationMinutes = 30;
    private const int HiitDurationMinutes = 20;
    private const int DefaultConditioningDurationMinutes = 30;
    private const int RecoverySortOrder = 2;
    private const int CardioSortOrder = 1;
    private const int StrengthSortOrder = 0;

    private static readonly CyclePhase[] PhaseOrder =
    [
        CyclePhase.Menstrual,
        CyclePhase.Follicular,
        CyclePhase.Ovulatory,
        CyclePhase.Luteal
    ];

    public static IReadOnlyList<PlannedWeekPeriod> Create(CyclePhase startingPhase, DateTime startMonday)
    {
        var phaseIndex = Array.IndexOf(PhaseOrder, startingPhase);
        if (phaseIndex < 0) phaseIndex = 0;

        return Enumerable.Range(0, WeekCount)
            .Select(index => new PlannedWeekPeriod(
                index,
                PhaseOrder[(phaseIndex + index) % PhaseOrder.Length],
                WorkoutScheduleMathRule.SafeAddDays(startMonday, index * DaysPerWeek)))
            .ToArray();
    }

    public static DailyInputs BuildPlanningInputs(DateOnly date, AdaptiveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new DailyInputs(
            date,
            DefaultSleepHours,
            DefaultSleepHours,
            profile.BaselineSteps,
            profile.BaselineSteps,
            DefaultEnergyLevel,
            DefaultPainLevel,
            false,
            null);
    }

    public static string BuildPlanName(CyclePhase phase, bool allowsAdjustments = true) =>
        allowsAdjustments
            ? $"Personalized {phase.ToString().ToLowerInvariant()} training"
            : "Personalized training plan";

    public static string BuildStrengthName(string archetypeCode, WorkoutActivityType activity, int weekIndex = 0)
    {
        var prefix = activity == WorkoutActivityType.StrengthHighIntensity ? "Heavy" : "Progressive";
        var baseName = archetypeCode switch
        {
            "P" => $"{prefix} glute and pull strength",
            "S" => $"{prefix} leg strength",
            "U" => $"{prefix} upper body strength",
            "U2" => $"{prefix} upper body and glute strength",
            "G" => "Glute accessory training",
            "F" => $"{prefix} full body strength",
            _ => $"{prefix} strength training"
        };
        return weekIndex > 0 ? $"{baseName} · Week {weekIndex + 1}" : baseName;
    }

    public static string BuildActivityName(WorkoutActivityType activity, int weekIndex = 0)
    {
        var baseName = activity switch
        {
            WorkoutActivityType.RockClimbing => "Climbing strength session",
            WorkoutActivityType.Hiit => "HIIT conditioning",
            WorkoutActivityType.Cycling => "Cycling conditioning",
            WorkoutActivityType.Running => "Running conditioning",
            WorkoutActivityType.Swimming => "Swimming conditioning",
            _ => "Conditioning session"
        };
        return weekIndex > 0 ? $"{baseName} · Week {weekIndex + 1}" : baseName;
    }

    public static string BuildRecoveryName(int weekIndex = 0) =>
        weekIndex > 0 ? $"{RecoveryDayBaseName} · Week {weekIndex + 1}" : RecoveryDayBaseName;

    public static string ResolveActivityExerciseName(WorkoutActivityType activity, CyclePhase phase) =>
        activity switch
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

    public static int GetActivityDurationMinutes(WorkoutActivityType activity) =>
        activity switch
        {
            WorkoutActivityType.RockClimbing => RockClimbingDurationMinutes,
            WorkoutActivityType.Running => RunningDurationMinutes,
            WorkoutActivityType.Cycling => CyclingDurationMinutes,
            WorkoutActivityType.Swimming => SwimmingDurationMinutes,
            WorkoutActivityType.Hiit => HiitDurationMinutes,
            _ => DefaultConditioningDurationMinutes
        };

    public static int GetActivityDurationSeconds(WorkoutActivityType activity) =>
        GetActivityDurationMinutes(activity) * SecondsPerMinute;

    public static InjuryFlag ToInjuryFlags(IEnumerable<Injury>? injuries)
    {
        if (injuries is null) return InjuryFlag.None;
        var flags = InjuryFlag.None;
        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
            flags |= injury.Site;
        return flags;
    }

    public static IReadOnlySet<WorkoutActivityType> GetBlockedActivities(IEnumerable<Injury>? injuries)
    {
        var blocked = new HashSet<WorkoutActivityType>();
        if (injuries is null) return blocked;

        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
        {
            if (injury.Site is InjuryFlag.Metatarsal or InjuryFlag.Ankle)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                if (injury.Status == InjuryStatus.Acute)
                {
                    blocked.Add(WorkoutActivityType.Running);
                    blocked.Add(WorkoutActivityType.RockClimbing);
                }
            }

            if (injury.Site == InjuryFlag.Knee)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                blocked.Add(WorkoutActivityType.Running);
                if (injury.Status == InjuryStatus.Acute) blocked.Add(WorkoutActivityType.RockClimbing);
            }

            if (injury.Site == InjuryFlag.Shoulder && injury.Status == InjuryStatus.Acute)
            {
                blocked.Add(WorkoutActivityType.Swimming);
                blocked.Add(WorkoutActivityType.RockClimbing);
            }

            if (injury.Site == InjuryFlag.Hip)
            {
                blocked.Add(WorkoutActivityType.Running);
                if (injury.Status == InjuryStatus.Acute)
                {
                    blocked.Add(WorkoutActivityType.Hiit);
                    blocked.Add(WorkoutActivityType.RockClimbing);
                }
            }

            if (injury.Site == InjuryFlag.Neck && injury.Status == InjuryStatus.Acute)
                blocked.Add(WorkoutActivityType.Swimming);

            if (injury.Site == InjuryFlag.Wrist && injury.Status == InjuryStatus.Acute)
                blocked.Add(WorkoutActivityType.RockClimbing);

            if (injury.Site == InjuryFlag.LowBack && injury.Status == InjuryStatus.Acute)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                blocked.Add(WorkoutActivityType.Running);
                blocked.Add(WorkoutActivityType.RockClimbing);
            }
        }

        return blocked;
    }

    public static string NormalizeExerciseKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public static GeneratedFourWeekPlan GeneratePlan(
        WorkoutPlanningSnapshot snapshot,
        AdaptiveProfile adaptiveProfile,
        CyclePhase phase,
        bool allowsCyclePhaseAdjustments,
        DateTime createdAt,
        DateOnly today,
        DailyInputs? currentDayInputs = null,
        int consecutiveLowDays = 0,
        WorkoutPlanningConfig? planningConfig = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(adaptiveProfile);

        if (snapshot.Exercises is null || snapshot.Exercises.Count == 0)
            throw new InvalidOperationException("The exercise catalog is empty; no replacement plan was generated.");
        if (snapshot.ExerciseDefinitions is null || snapshot.ExerciseDefinitions.Count == 0)
            throw new InvalidOperationException("Workout planning reference data is incomplete or malformed.");

        var exerciseLookup = BuildExerciseLookup(snapshot.Exercises);
        if (exerciseLookup.Count == 0)
            throw new InvalidOperationException("The exercise catalog has no usable references; no replacement plan was generated.");

        var daysPerWeek = WeekPlanRule.ClampDaysPerWeek(adaptiveProfile.DaysPerWeek);
        var template = snapshot.WeekTemplates.FirstOrDefault(item => item.DaysPerWeek == daysPerWeek);
        if (!WeekPlanRule.TryValidateTemplate(template, daysPerWeek, out var archetypeSequence, out _))
            throw new InvalidOperationException("Workout planning reference data is incomplete or malformed.");

        var strengthIds = archetypeSequence.Where(id => id > 0).Distinct().ToArray();
        var matchingArchetypes = snapshot.Archetypes.Where(item => strengthIds.Contains(item.Id)).ToArray();
        if (!WeekPlanRule.TryValidateArchetypes(matchingArchetypes, strengthIds, daysPerWeek, out var archetypesById, out _))
            throw new InvalidOperationException("Workout planning reference data is incomplete or malformed.");

        var referenceIndex = new WorkoutPlanningReferenceIndex(snapshot);
        var tierAB = WorkoutPlanningReferenceIndex.GoalMuscles(snapshot, adaptiveProfile.Goal, tierCOnly: false);
        var tierC = adaptiveProfile.SessionMinutesCap <= 35
            ? WorkoutPlanningReferenceIndex.GoalMuscles(snapshot, adaptiveProfile.Goal, tierCOnly: true)
            : null;
        var indexedVolumes = WorkoutPlanningReferenceIndex.VolumeRows(snapshot, daysPerWeek);
        var budget = VolumeBudgetRule.Resolve(
            indexedVolumes.Select(row => new VolumeBudgetObservation(row.Key, row.Value)).ToArray(),
            adaptiveProfile.Experience,
            adaptiveProfile.SessionMinutesCap,
            tierC);
        var exposureMap = WeekPlanRule.ComputeArchetypeExposures(
            archetypesById,
            snapshot.ExerciseDefinitions,
            referenceIndex,
            out var patternMuscleMap);

        var safeCreatedAt = WorkoutScheduleMathRule.ClampSafeDate(createdAt);
        var startMonday = WorkoutScheduleMathRule.GetSafeWeekStart(safeCreatedAt);
        var neutralInputs = BuildPlanningInputs(today, adaptiveProfile);
        var todayInputs = currentDayInputs ?? neutralInputs;
        var equipment = ExercisePickerRule.MapEquipment(adaptiveProfile.Equipment);
        var injuryFlags = ToInjuryFlags(adaptiveProfile.Injuries);
        var blockedActivities = GetBlockedActivities(adaptiveProfile.Injuries);
        var effectivePhase = allowsCyclePhaseAdjustments ? phase : CyclePhase.Follicular;
        var config = planningConfig ?? WorkoutPlanningConfig.CreateDefault();
        var preferences = snapshot.ExerciseSubstitutions
            .GroupBy(item => item.OriginalExerciseId)
            .ToDictionary(group => group.Key, group => group.First().PreferredExerciseId);

        var generatedDays = new List<GeneratedWorkoutDay>();

        foreach (var period in Create(phase, startMonday))
        {
            var week = WeekPlanRule.BuildWeek(
                new WeekGenerationInput
                {
                    DaysPerWeek = adaptiveProfile.DaysPerWeek,
                    Experience = adaptiveProfile.Experience,
                    Goal = adaptiveProfile.Goal,
                    SessionMinutesTarget = adaptiveProfile.SessionMinutesCap,
                    SelectedActivities = adaptiveProfile.Selected,
                    HasRunningOrClimbing = adaptiveProfile.Selected.Contains(WorkoutActivityType.Running) ||
                                           adaptiveProfile.Selected.Contains(WorkoutActivityType.RockClimbing),
                    Phase = period.Phase
                },
                daysPerWeek,
                archetypeSequence,
                archetypesById,
                tierAB,
                budget,
                exposureMap,
                patternMuscleMap);

            if (WeekPlanRule.IsCatalogFailure(week) || (week.Validation.HasErrors && week.Sessions.Count == 0))
                throw new InvalidOperationException("A safe workout plan could not be generated from the available planning data.");

            var activeDaysInWeek = new HashSet<int>();

            foreach (var session in week.Sessions
                         .OrderBy(session => (int)session.Day)
                         .ThenBy(session => session.IsRecovery ? RecoverySortOrder : session.IsCardio ? CardioSortOrder : StrengthSortOrder))
            {
                var dayOffset = (DaysPerWeek + (int)session.Day - (int)DayOfWeek.Monday) % DaysPerWeek;
                var sessionDate = WorkoutScheduleMathRule.SafeAddDays(period.StartDate, dayOffset);
                var isToday = DateOnly.FromDateTime(sessionDate) == today;
                var inputs = isToday ? todayInputs : neutralInputs;
                var dayIndex = (int)session.Day;
                activeDaysInWeek.Add(dayIndex);

                if (session.IsRecovery)
                {
                    generatedDays.Add(BuildRecoveryDay(session, adaptiveProfile, exerciseLookup, period.WeekIndex, sessionDate));
                    continue;
                }

                if (session.IsCardio && session.CardioActivity is not null)
                {
                    var activity = session.CardioActivity.Value;
                    if (isToday)
                    {
                        var readiness = ReadinessRule.Compute(inputs, period.Phase, config, adaptiveProfile.Baselines);
                        activity = WorkoutGatingRule.Resolve(
                            WorkoutType.Cardio,
                            adaptiveProfile,
                            inputs,
                            readiness,
                            period.Phase,
                            consecutiveLowDays,
                            config).Activity;
                    }

                    if (blockedActivities.Contains(activity))
                        activity = WorkoutActivityType.Yoga;

                    var day = activity == WorkoutActivityType.Yoga
                        ? BuildRecoveryDay(session, adaptiveProfile, exerciseLookup, period.WeekIndex, sessionDate)
                        : BuildConditioningDay(session, activity, period.Phase, exerciseLookup, period.WeekIndex, sessionDate);
                    generatedDays.Add(day);
                    continue;
                }

                var strengthReadiness = ReadinessRule.Compute(inputs, period.Phase, config, adaptiveProfile.Baselines);
                var strengthGating = WorkoutGatingRule.Resolve(
                    WorkoutType.Strength,
                    adaptiveProfile,
                    inputs,
                    strengthReadiness,
                    period.Phase,
                    isToday ? consecutiveLowDays : 0,
                    config);

                generatedDays.Add(BuildStrengthDay(
                    session,
                    strengthGating,
                    equipment,
                    injuryFlags,
                    snapshot.ExerciseDefinitions,
                    preferences,
                    referenceIndex,
                    exerciseLookup,
                    period.WeekIndex,
                    sessionDate));
            }

            foreach (var dayOfWeek in Enumerable.Range(0, DaysPerWeek).Where(day => !activeDaysInWeek.Contains(day)))
            {
                var dayOffset = (DaysPerWeek + dayOfWeek - (int)DayOfWeek.Monday) % DaysPerWeek;
                var restDate = WorkoutScheduleMathRule.SafeAddDays(period.StartDate, dayOffset);
                generatedDays.Add(new GeneratedWorkoutDay(
                    dayOfWeek,
                    period.WeekIndex,
                    restDate,
                    WorkoutType.Rest,
                    RestDayName,
                    []));
            }
        }

        return new GeneratedFourWeekPlan(
            BuildPlanName(effectivePhase, allowsCyclePhaseAdjustments),
            effectivePhase,
            safeCreatedAt,
            generatedDays);
    }

    private static GeneratedWorkoutDay BuildStrengthDay(
        PlannedSession session,
        GatingResult gating,
        EquipmentSet equipment,
        InjuryFlag injuries,
        IReadOnlyList<PlanningExerciseDefinition> allDefinitions,
        IReadOnlyDictionary<int, int> preferences,
        WorkoutPlanningReferenceIndex referenceIndex,
        IReadOnlyDictionary<string, PlanningExercise> exerciseLookup,
        int weekIndex,
        DateTime sessionDate)
    {
        if (gating.SetMultiplier <= 0)
        {
            return new GeneratedWorkoutDay(
                (int)session.Day,
                weekIndex,
                sessionDate,
                WorkoutType.Rest,
                RestDayName,
                []);
        }

        var patterns = session.Slots.SelectMany(slot => slot.AllowedPatterns).Distinct().ToHashSet();
        // The legacy filtered catalog read yields candidates grouped by movement-pattern index;
        // retain that order so equal-scoring picks remain stable after assembling a full snapshot.
        var matchingDefinitions = allDefinitions
            .Where(definition => patterns.Contains(definition.Pattern))
            .OrderBy(definition => definition.Pattern)
            .ThenBy(definition => definition.Id)
            .ToArray();
        if (matchingDefinitions.Length == 0)
            throw new InvalidOperationException("No exercise definitions match the planned movement patterns.");

        var candidates = ExercisePickerRule.FilterCandidates(matchingDefinitions, equipment, injuries);
        if (candidates.Count == 0)
            throw new InvalidOperationException("No safe exercise can be selected for the planned session.");

        var prescribed = ExercisePickerRule.PrescribeSession(
            session,
            candidates,
            injuries,
            gating.SetMultiplier,
            gating.RpeCap,
            preferences,
            referenceIndex) ?? throw new InvalidOperationException("The planned strength session has no usable exercises.");

        var exercises = prescribed.Exercises
            .Select(picked =>
            {
                var resolved = ResolveExercise(exerciseLookup, picked.ExerciseId, picked.ExerciseName);
                return new GeneratedWorkoutDayExercise(resolved.Id, resolved.Code, resolved.Name, picked.Sets, picked.RepsMax, null);
            })
            .ToArray();

        return new GeneratedWorkoutDay(
            (int)session.Day,
            weekIndex,
            sessionDate,
            WorkoutType.Strength,
            BuildStrengthName(session.ArchetypeCode, gating.Activity, weekIndex),
            exercises);
    }

    private static GeneratedWorkoutDay BuildConditioningDay(
        PlannedSession planned,
        WorkoutActivityType activity,
        CyclePhase phase,
        IReadOnlyDictionary<string, PlanningExercise> exerciseLookup,
        int weekIndex,
        DateTime sessionDate)
    {
        var exerciseName = ResolveActivityExerciseName(activity, phase);
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);
        return new GeneratedWorkoutDay(
            (int)planned.Day,
            weekIndex,
            sessionDate,
            activity == WorkoutActivityType.RockClimbing ? WorkoutType.Strength : WorkoutType.Cardio,
            BuildActivityName(activity, weekIndex),
            [new GeneratedWorkoutDayExercise(exercise.Id, exercise.Code, exercise.Name, 0, 0, GetActivityDurationSeconds(activity))]);
    }

    private static GeneratedWorkoutDay BuildRecoveryDay(
        PlannedSession planned,
        AdaptiveProfile profile,
        IReadOnlyDictionary<string, PlanningExercise> exerciseLookup,
        int weekIndex,
        DateTime sessionDate)
    {
        var exerciseName = profile.Selected.Contains(WorkoutActivityType.Yoga) ? "Yoga Flow" : "Mobility Flow";
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);
        return new GeneratedWorkoutDay(
            (int)planned.Day,
            weekIndex,
            sessionDate,
            WorkoutType.Recovery,
            BuildRecoveryName(weekIndex),
            [new GeneratedWorkoutDayExercise(exercise.Id, exercise.Code, exercise.Name, 0, 0, RecoverySessionDurationSeconds)]);
    }

    private static Dictionary<string, PlanningExercise> BuildExerciseLookup(
        IReadOnlyCollection<PlanningExercise> exercises)
    {
        var lookup = new Dictionary<string, PlanningExercise>(StringComparer.OrdinalIgnoreCase);
        foreach (var exercise in exercises)
        {
            lookup.TryAdd(exercise.Code, exercise);
            lookup.TryAdd(NormalizeExerciseKey(exercise.Name), exercise);
        }

        return lookup;
    }

    private static PlanningExercise ResolveExercise(
        IReadOnlyDictionary<string, PlanningExercise> lookup,
        int? planningExerciseId,
        string name)
    {
        if (planningExerciseId is not null)
        {
            if (lookup.TryGetValue($"ENGINE_{planningExerciseId}", out var byPlanningExerciseId) &&
                NormalizeExerciseKey(byPlanningExerciseId.Name) == NormalizeExerciseKey(name))
                return byPlanningExerciseId;
            if (lookup.TryGetValue(NormalizeExerciseKey(name), out var byMatchingName))
                return byMatchingName;
            throw new InvalidOperationException("A selected exercise reference is missing from the catalog.");
        }

        if (lookup.TryGetValue(NormalizeExerciseKey(name), out var byName))
            return byName;

        throw new InvalidOperationException("A required workout exercise is missing from the catalog.");
    }
}

public sealed record PlannedWeekPeriod(int WeekIndex, CyclePhase Phase, DateTime StartDate);

public sealed record GeneratedFourWeekPlan(
    string Name,
    CyclePhase CyclePhaseTarget,
    DateTime CreatedAt,
    IReadOnlyList<GeneratedWorkoutDay> WorkoutDays);

public sealed record GeneratedWorkoutDay(
    int DayOfWeek,
    int WeekIndex,
    DateTime Date,
    WorkoutType WorkoutType,
    string Name,
    IReadOnlyList<GeneratedWorkoutDayExercise> Exercises);

public sealed record GeneratedWorkoutDayExercise(
    int ExerciseId,
    string ExerciseCode,
    string ExerciseName,
    int Sets,
    int Reps,
    int? DurationSeconds);
