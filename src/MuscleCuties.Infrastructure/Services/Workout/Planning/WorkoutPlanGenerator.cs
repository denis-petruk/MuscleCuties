using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWorkoutPlanGenerator
{
    bool ShouldRegenerate(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        CyclePhase phase,
        IReadOnlyCollection<WorkoutInjuryLog>? activeInjuries = null);

    Task<WorkoutPlan> GenerateAsync(
        UserProfile profile,
        CyclePhase phase,
        DateTime createdAt);

    Task<WorkoutPlan> GenerateAsync(
        UserProfile profile,
        CyclePhase phase,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GenerateAsync(profile, phase, createdAt);
    }
}

public sealed class WorkoutPlanGenerator : IWorkoutPlanGenerator
{
    private const string PlanNamePrefix = "Personalized";
    private const int DefaultDaysPerWeek = 3;
    private const int DaysInWeek = 7;
    private const int RecoverySortOrder = 2;
    private const int CardioSortOrder = 1;
    private const int StrengthSortOrder = 0;

    private readonly IReadinessRepository _dailyRepository;
    private readonly IWorkoutPlanningDataSource _dataSource;
    private readonly IExercisePickerService _exercisePicker;
    private readonly IGatingEngine _gating;
    private readonly IWorkoutInjuryRepository _injuryRepository;
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
        : this(new AppDatabaseWorkoutPlanningDataSource(database), weekGenerator, exercisePicker,
            injuryRepository, dailyRepository, readiness, gating)
    {
    }

    public WorkoutPlanGenerator(
        IWorkoutPlanningDataSource dataSource,
        IWeekPlanGenerator weekGenerator,
        IExercisePickerService exercisePicker,
        IWorkoutInjuryRepository injuryRepository,
        IReadinessRepository dailyRepository,
        IReadinessEngine readiness,
        IGatingEngine gating)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _weekGenerator = weekGenerator ?? throw new ArgumentNullException(nameof(weekGenerator));
        _exercisePicker = exercisePicker ?? throw new ArgumentNullException(nameof(exercisePicker));
        _injuryRepository = injuryRepository ?? throw new ArgumentNullException(nameof(injuryRepository));
        _dailyRepository = dailyRepository ?? throw new ArgumentNullException(nameof(dailyRepository));
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        _gating = gating ?? throw new ArgumentNullException(nameof(gating));
    }

    public bool ShouldRegenerate(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        CyclePhase phase,
        IReadOnlyCollection<WorkoutInjuryLog>? activeInjuries = null)
    {
        if (!WorkoutPlanGenerationRule.CanGeneratePlans)
            return false;
        if (activePlan is null)
            return true;

        if (activePlanDays.Count == 0)
            return true;

        if (!IsGenerated(activePlan))
            return false;

        var expectedActiveDays = WeekPlanRule.ClampDaysPerWeek(
            profile.WorkoutDaysPerWeek <= 0 ? DefaultDaysPerWeek : profile.WorkoutDaysPerWeek);
        var hasMismatchedWeek = activePlanDays
            .GroupBy(day => day.WeekIndex)
            .Any(week => week.Select(day => day.DayOfWeek).Distinct().Count() != DaysInWeek ||
                         week.Count(day => day.WorkoutType != WorkoutType.Rest) != expectedActiveDays);
        var reproductiveContext = ReproductiveContextCodec.Parse(profile.ReproductiveContextJson);
        var expectedPhase = reproductiveContext.AllowsCyclePhaseTrainingAdjustments ? phase : CyclePhase.Follicular;
        var expectedName = FourWeekPlanRule.BuildPlanName(expectedPhase, reproductiveContext.AllowsCyclePhaseTrainingAdjustments);
        if (activePlan.CyclePhaseTarget != expectedPhase ||
            !activePlan.Name.Equals(expectedName, StringComparison.Ordinal) ||
            hasMismatchedWeek ||
            activePlanDays.Any(day => day.WorkoutType != WorkoutType.Rest && day.WorkoutDayExercises.Count == 0))
            return true;

        if (activeInjuries is not null)
        {
            if (activeInjuries.Any(injury => injury.CreatedAt > activePlan.CreatedAt)) return true;

            var mappedInjuries = WorkoutInjuryRules.MapInjuries(activeInjuries);
            var flags = FourWeekPlanRule.ToInjuryFlags(mappedInjuries);
            if (flags != InjuryFlag.None)
            {
                var blockedActivities = FourWeekPlanRule.GetBlockedActivities(mappedInjuries);
                foreach (var day in activePlanDays)
                    if (day.WorkoutType == WorkoutType.Cardio)
                    {
                        var hasBlockedCardio = blockedActivities.Any(act =>
                            day.Name.Contains(act.ToString(), StringComparison.OrdinalIgnoreCase) ||
                            day.WorkoutDayExercises.Any(e =>
                                e.Exercise?.Name.Contains(act.ToString(), StringComparison.OrdinalIgnoreCase) == true));
                        if (hasBlockedCardio)
                            return true;
                    }
            }
        }

        return false;
    }

    public Task<WorkoutPlan> GenerateAsync(UserProfile profile, CyclePhase phase, DateTime createdAt) =>
        GenerateAsync(profile, phase, createdAt, CancellationToken.None);

    public async Task<WorkoutPlan> GenerateAsync(
        UserProfile profile,
        CyclePhase phase,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WorkoutPlanGenerationRule.CanGeneratePlans)
            throw new WorkoutPlanningFailureException(WorkoutPlanGenerationRule.GenerationUnavailableMessage);
        await _dataSource.EnsurePlanningCatalogAsync(cancellationToken).ConfigureAwait(false);
        var exerciseLibrary = await _dataSource.GetExerciseLibraryAsync(cancellationToken).ConfigureAwait(false);
        if (exerciseLibrary is null || exerciseLibrary.Count == 0)
            throw new WorkoutPlanningFailureException("The exercise catalog is empty; no replacement plan was generated.");

        cancellationToken.ThrowIfCancellationRequested();
        var injuries = await _injuryRepository.GetActiveAsync(profile.UserId).ConfigureAwait(false);
        var adaptiveProfile = AdaptiveProfileMapper.FromUserProfile(profile, injuries);

        var safeCreatedAt = ClampSafeDate(createdAt);
        var startMonday = GetSafeWeekStart(safeCreatedAt);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var neutralInputs = FourWeekPlanRule.BuildPlanningInputs(today, adaptiveProfile);
        var currentInputs = await _dailyRepository.GetInputsForAsync(profile.UserId, today).ConfigureAwait(false) ??
                            neutralInputs;
        cancellationToken.ThrowIfCancellationRequested();
        var consecutiveLowDays = await _dailyRepository
            .GetConsecutiveLowDaysAsync(profile.UserId, today)
            .ConfigureAwait(false);
        var equipment = ExercisePickerRule.MapEquipment(adaptiveProfile.Equipment);
        var injuryFlags = FourWeekPlanRule.ToInjuryFlags(adaptiveProfile.Injuries);
        var blockedActivities = FourWeekPlanRule.GetBlockedActivities(adaptiveProfile.Injuries);
        var exerciseLookup = BuildExerciseLookup(exerciseLibrary);
        if (exerciseLookup.Count == 0)
            throw new WorkoutPlanningFailureException("The exercise catalog has no usable references; no replacement plan was generated.");

        var reproductiveContext = ReproductiveContextCodec.Parse(profile.ReproductiveContextJson);
        var effectivePhase = reproductiveContext.AllowsCyclePhaseTrainingAdjustments ? phase : CyclePhase.Follicular;

        var plan = new WorkoutPlan
        {
            UserId = profile.UserId,
            Name = FourWeekPlanRule.BuildPlanName(effectivePhase, reproductiveContext.AllowsCyclePhaseTrainingAdjustments),
            IsActive = true,
            CyclePhaseTarget = effectivePhase,
            CreatedAt = safeCreatedAt
        };

        foreach (var period in FourWeekPlanRule.Create(phase, startMonday))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var weekIndex = period.WeekIndex;
            var weekPhase = period.Phase;
            var weekStartDate = period.StartDate;

            var week = await _weekGenerator.GenerateWeekAsync(new WeekGenerationInput
            {
                DaysPerWeek = adaptiveProfile.DaysPerWeek,
                Experience = adaptiveProfile.Experience,
                Goal = adaptiveProfile.Goal,
                SessionMinutesTarget = adaptiveProfile.SessionMinutesCap,
                SelectedActivities = adaptiveProfile.Selected,
                HasRunningOrClimbing = adaptiveProfile.Selected.Contains(WorkoutActivityType.Running) ||
                                       adaptiveProfile.Selected.Contains(WorkoutActivityType.RockClimbing),
                Phase = weekPhase
            }, cancellationToken).ConfigureAwait(false);

            if (WorkoutPlanningCatalogException.IsCatalogFailure(week))
                throw new WorkoutPlanningCatalogException();

            if (week.Validation.HasErrors && week.Sessions.Count == 0)
                throw new WorkoutPlanningFailureException();

            var activeDaysInWeek = new HashSet<int>();

            foreach (var session in week.Sessions
                         .OrderBy(session => ToDayIndex(session.Day))
                         .ThenBy(session => session.IsRecovery ? RecoverySortOrder : session.IsCardio ? CardioSortOrder : StrengthSortOrder))
            {
                var dayOffset = DayOffsetFromMonday(session.Day);
                var sessionDate = SafeAddDays(weekStartDate, dayOffset);
                var isToday = sessionDate.Date == DateTime.Today;
                var inputs = isToday ? currentInputs : neutralInputs;
                var dayIndex = ToDayIndex(session.Day);
                activeDaysInWeek.Add(dayIndex);

                if (session.IsRecovery)
                {
                    plan.WorkoutDays.Add(BuildRecoveryDay(session, adaptiveProfile, exerciseLookup, weekIndex,
                        sessionDate));
                    continue;
                }

                if (session.IsCardio && session.CardioActivity is not null)
                {
                    var activity = session.CardioActivity.Value;
                    if (isToday)
                    {
                        var readiness = _readiness.Compute(inputs, weekPhase, adaptiveProfile.Baselines);
                        activity = _gating.Resolve(
                            WorkoutType.Cardio,
                            adaptiveProfile,
                            inputs,
                            readiness,
                            weekPhase,
                            consecutiveLowDays).Activity;
                    }

                    if (blockedActivities.Contains(activity))
                        activity = WorkoutActivityType.Yoga;

                    var day = activity == WorkoutActivityType.Yoga
                        ? BuildRecoveryDay(session, adaptiveProfile, exerciseLookup, weekIndex, sessionDate)
                        : BuildConditioningDay(session, activity, weekPhase, exerciseLookup, weekIndex, sessionDate);
                    plan.WorkoutDays.Add(day);
                    continue;
                }

                var strengthReadiness = _readiness.Compute(inputs, weekPhase, adaptiveProfile.Baselines);
                var strengthGating = _gating.Resolve(
                    WorkoutType.Strength,
                    adaptiveProfile,
                    inputs,
                    strengthReadiness,
                    weekPhase,
                    isToday ? consecutiveLowDays : 0);
                var strengthDay = await BuildStrengthDayAsync(
                    session,
                    strengthGating,
                    equipment,
                    injuryFlags,
                    exerciseLookup,
                    profile.UserId,
                    weekIndex,
                    sessionDate,
                    cancellationToken).ConfigureAwait(false);
                plan.WorkoutDays.Add(strengthDay);
            }

            foreach (var dayOfWeek in Enumerable.Range(0, DaysInWeek).Where(day => !activeDaysInWeek.Contains(day)))
            {
                var dayOffset = DayOffsetFromMonday((DayOfWeek)dayOfWeek);
                var restDate = SafeAddDays(weekStartDate, dayOffset);
                plan.WorkoutDays.Add(new WorkoutDay
                {
                    DayOfWeek = dayOfWeek,
                    WeekIndex = weekIndex,
                    Date = restDate,
                    WorkoutType = WorkoutType.Rest,
                    Name = FourWeekPlanRule.RestDayName
                });
            }
        }

        return plan;
    }

    private async Task<WorkoutDay> BuildStrengthDayAsync(
        PlannedSession session,
        GatingResult gating,
        EquipmentSet equipment,
        InjuryFlag injuries,
        IReadOnlyDictionary<string, Exercise> exerciseLookup,
        int userId,
        int weekIndex,
        DateTime sessionDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (gating.SetMultiplier <= 0)
            return new WorkoutDay
            {
                DayOfWeek = ToDayIndex(session.Day),
                WeekIndex = weekIndex,
                Date = sessionDate,
                WorkoutType = WorkoutType.Rest,
                Name = FourWeekPlanRule.RestDayName
            };

        var prescribed = await _exercisePicker.PickForSessionAsync(
            session,
            equipment,
            injuries,
            gating.SetMultiplier,
            gating.RpeCap,
            userId,
            cancellationToken);

        var day = new WorkoutDay
        {
            DayOfWeek = ToDayIndex(session.Day),
            WeekIndex = weekIndex,
            Date = sessionDate,
            WorkoutType = WorkoutType.Strength,
            Name = FourWeekPlanRule.BuildStrengthName(session.ArchetypeCode, gating.Activity, weekIndex)
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

    private static WorkoutDay BuildConditioningDay(
        PlannedSession planned,
        WorkoutActivityType activity,
        CyclePhase phase,
        IReadOnlyDictionary<string, Exercise> exerciseLookup,
        int weekIndex,
        DateTime sessionDate)
    {
        var exerciseName = FourWeekPlanRule.ResolveActivityExerciseName(activity, phase);
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);

        return new WorkoutDay
        {
            DayOfWeek = ToDayIndex(planned.Day),
            WeekIndex = weekIndex,
            Date = sessionDate,
            WorkoutType = activity == WorkoutActivityType.RockClimbing
                ? WorkoutType.Strength
                : WorkoutType.Cardio,
            Name = FourWeekPlanRule.BuildActivityName(activity, weekIndex),
            WorkoutDayExercises =
            [
                new WorkoutDayExercise
                {
                    ExerciseId = exercise.Id,
                    DurationSeconds = FourWeekPlanRule.GetActivityDurationSeconds(activity)
                }
            ]
        };
    }

    private static WorkoutDay BuildRecoveryDay(
        PlannedSession planned,
        AdaptiveProfile profile,
        IReadOnlyDictionary<string, Exercise> exerciseLookup,
        int weekIndex,
        DateTime sessionDate)
    {
        var exerciseName = profile.Selected.Contains(WorkoutActivityType.Yoga) ? "Yoga Flow" : "Mobility Flow";
        var exercise = ResolveExercise(exerciseLookup, null, exerciseName);

        return new WorkoutDay
        {
            DayOfWeek = ToDayIndex(planned.Day),
            WeekIndex = weekIndex,
            Date = sessionDate,
            WorkoutType = WorkoutType.Recovery,
            Name = FourWeekPlanRule.BuildRecoveryName(weekIndex),
            WorkoutDayExercises =
            [
                new WorkoutDayExercise
                {
                    ExerciseId = exercise.Id,
                    DurationSeconds = FourWeekPlanRule.RecoverySessionDurationSeconds
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
            lookup.TryAdd(FourWeekPlanRule.NormalizeExerciseKey(exercise.Name), exercise);
        }

        return lookup;
    }

    private static Exercise ResolveExercise(
        IReadOnlyDictionary<string, Exercise> lookup,
        int? planningExerciseId,
        string name)
    {
        if (planningExerciseId is not null)
        {
            if (lookup.TryGetValue($"ENGINE_{planningExerciseId}", out var byPlanningExerciseId))
                return byPlanningExerciseId;

            throw new WorkoutPlanningFailureException("A selected exercise reference is missing from the catalog.");
        }

        if (lookup.TryGetValue(FourWeekPlanRule.NormalizeExerciseKey(name), out var byName))
            return byName;

        throw new WorkoutPlanningFailureException("A required workout exercise is missing from the catalog.");
    }

    private static bool IsGenerated(WorkoutPlan plan)
    {
        return plan.CyclePhaseTarget.HasValue ||
               plan.Name.StartsWith(PlanNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Generated training", StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Glow Plan", StringComparison.OrdinalIgnoreCase) ||
               plan.Name.StartsWith("Cycle Plan", StringComparison.OrdinalIgnoreCase);
    }

    private static int ToDayIndex(DayOfWeek day) => (int)day;

    private static int DayOffsetFromMonday(DayOfWeek day) =>
        (DaysInWeek + (int)day - (int)DayOfWeek.Monday) % DaysInWeek;

    private static DateTime ClampSafeDate(DateTime date) =>
        WorkoutScheduleService.ClampSafeDate(date);

    private static DateTime GetSafeWeekStart(DateTime date) =>
        WorkoutScheduleService.GetSafeWeekStart(date);

    private static DateTime SafeAddDays(DateTime baseDate, int days) =>
        WorkoutScheduleService.SafeAddDays(baseDate, days);
}
