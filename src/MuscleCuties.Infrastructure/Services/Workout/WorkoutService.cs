using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout;

public class WorkoutService : IWorkoutService
{
    private readonly IContributionLookup _contributions;
    private readonly AppDatabase _db;
    private readonly IWorkoutInjuryRepository _injuryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWorkoutPlanner _workoutPlanner;
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IWorkoutScheduleService _workoutScheduleService;

    public WorkoutService(
        AppDatabase db,
        IContributionLookup contributions,
        IWorkoutRepository workoutRepository,
        IUserRepository userRepository,
        IWorkoutInjuryRepository injuryRepository,
        IWorkoutPlanner workoutPlanner,
        IWorkoutScheduleService? workoutScheduleService = null)
    {
        _db = db;
        _contributions = contributions;
        _workoutRepository = workoutRepository;
        _userRepository = userRepository;
        _injuryRepository = injuryRepository;
        _workoutPlanner = workoutPlanner;
        _workoutScheduleService = workoutScheduleService ?? new WorkoutScheduleService(db, workoutRepository);
    }

    public async Task<WorkoutPlanSummary> GetPlanSummaryAsync(int userId, CyclePhase phase)
    {
        var activePlan = await _workoutRepository.GetActivePlanAsync(userId);
        if (activePlan is null)
            return new WorkoutPlanSummary(null, [], []);

        var workoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id);
        var workoutDayIds = workoutDays.Select(day => day.Id).ToHashSet();
        var weekStart = GetWeekStart(DateTime.Today);
        var weekEnd = weekStart.AddDays(6);
        var workoutLogs = (await _workoutRepository.GetWorkoutLogsByDateRangeAsync(
                userId,
                weekStart,
                weekEnd))
            .Where(log => workoutDayIds.Contains(log.WorkoutDayId))
            .ToList();
        var workouts = _workoutPlanner.BuildWorkoutItems(workoutDays, workoutLogs);

        return new WorkoutPlanSummary(activePlan, workoutDays, workouts);
    }

    public async Task<TodaysWorkoutSummary> GetTodaysSummaryAsync(
        int userId,
        CyclePhase phase,
        DateTime date)
    {
        var activePlan = await _workoutRepository.GetActivePlanAsync(userId);
        if (activePlan is null)
            return TodaysWorkoutSummary.RestDay;

        var workoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id);
        var workoutLogs = await _workoutRepository.GetWorkoutLogsByDateAsync(userId, date);

        return _workoutPlanner.BuildTodaysSummary(activePlan, workoutDays, workoutLogs, phase, date);
    }

    public async Task<WorkoutSessionDetail> GetWorkoutSessionDetailAsync(int userId, int workoutDayId)
    {
        var day = await _workoutRepository.GetWorkoutDayWithExercisesAsync(workoutDayId);
        if (day is null)
            throw new InvalidOperationException("Workout session was not found.");

        if (day.WorkoutType is WorkoutType.Rest)
            return new WorkoutSessionDetail(
                day.Id,
                "Living happy life",
                "Pure rest day",
                "No exercises today. Log the rest if you actually took it.",
                [],
                true);

        var orderedExercises = day.WorkoutDayExercises
            .OrderBy(exercise => exercise.Id)
            .ToList();
        var exerciseIds = orderedExercises
            .Select(exercise => exercise.ExerciseId)
            .Distinct()
            .ToList();
        var currentLog = await _workoutRepository.GetWorkoutLogForDayAsync(userId, day.Id, DateTime.Today);
        var currentLogByDayExerciseId = (currentLog?.ExerciseLogs ?? [])
            .GroupBy(log => log.WorkoutDayExerciseId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(log => log.CreatedAt).First());
        var previousLogs = await _workoutRepository.GetExerciseLogsByExerciseIdsAsync(userId, exerciseIds);
        var latestLogByExercise = previousLogs
            .Where(log => currentLog is null || log.WorkoutLogId != currentLog.Id)
            .GroupBy(log => log.ExerciseId)
            .ToDictionary(group => group.Key, group => group.First());

        var notesByExerciseId = await _db.UserMovementNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId && exerciseIds.Contains(n.ExerciseId))
            .ToDictionaryAsync(n => n.ExerciseId, n => n.Notes)
            .ConfigureAwait(false);

        var exerciseItems = orderedExercises
            .Select(exercise => BuildExerciseItem(
                exercise,
                day.WorkoutType,
                latestLogByExercise.GetValueOrDefault(exercise.ExerciseId),
                currentLogByDayExerciseId.GetValueOrDefault(exercise.Id),
                notesByExerciseId.GetValueOrDefault(exercise.ExerciseId)))
            .ToList();
        var activitySections = BuildActivitySections(day, exerciseItems);

        return new WorkoutSessionDetail(
            day.Id,
            day.Name,
            BuildWorkoutDaySubtitle(day),
            BuildWorkoutDaySummary(day, orderedExercises),
            exerciseItems)
        {
            Activities = activitySections
        };
    }

    public async Task LogWorkoutSessionAsync(
        int userId,
        int workoutDayId,
        IReadOnlyCollection<WorkoutExerciseLogInput> exerciseLogs,
        DateTime date)
    {
        var day = await _workoutRepository.GetWorkoutDayWithExercisesAsync(workoutDayId);
        if (day is null)
            throw new InvalidOperationException("Workout session was not found.");

        if (day.WorkoutType is WorkoutType.Rest)
        {
            await _workoutRepository.ReplaceWorkoutLogAsync(new WorkoutLog
            {
                UserId = userId,
                WorkoutDayId = day.Id,
                Date = date.Date,
                CompletionPercent = 100,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        var exercisesByDayExerciseId = day.WorkoutDayExercises
            .ToDictionary(exercise => exercise.Id);
        var validExerciseLogs = exerciseLogs
            .Where(log => exercisesByDayExerciseId.ContainsKey(log.WorkoutDayExerciseId))
            .GroupBy(log => log.WorkoutDayExerciseId)
            .Select(group => group.Last())
            .ToList();

        if (validExerciseLogs.Count == 0)
            throw new InvalidOperationException("Log at least one exercise before saving this workout.");

        var createdAt = DateTime.UtcNow;
        var currentLog = await _workoutRepository.GetWorkoutLogForDayAsync(userId, day.Id, date);
        var alreadyLoggedIds = currentLog?.ExerciseLogs
            .Where(log =>
                exercisesByDayExerciseId.TryGetValue(log.WorkoutDayExerciseId, out var dayExercise) &&
                IsExerciseLogComplete(log, dayExercise, day.WorkoutType))
            .Select(log => log.WorkoutDayExerciseId)
            .ToHashSet() ?? [];
        foreach (var input in validExerciseLogs)
        {
            var dayExercise = exercisesByDayExerciseId[input.WorkoutDayExerciseId];
            ValidateExerciseLogInput(input, dayExercise, day.WorkoutType);
            alreadyLoggedIds.Add(input.WorkoutDayExerciseId);
        }

        var workoutLog = new WorkoutLog
        {
            UserId = userId,
            WorkoutDayId = day.Id,
            Date = date.Date,
            CompletionPercent = CalculateCompletionPercent(alreadyLoggedIds.Count, day.WorkoutDayExercises.Count),
            CreatedAt = createdAt
        };

        foreach (var input in validExerciseLogs)
        {
            var dayExercise = exercisesByDayExerciseId[input.WorkoutDayExerciseId];
            workoutLog.ExerciseLogs.Add(new WorkoutExerciseLog
            {
                WorkoutDayExerciseId = dayExercise.Id,
                ExerciseId = dayExercise.ExerciseId,
                CompletedSets = Math.Max(0, input.CompletedSets),
                CompletedReps = Math.Max(0, input.CompletedReps),
                WeightKg = input.WeightKg is >= 0f ? input.WeightKg : null,
                CompletedDurationSeconds =
                    input.CompletedDurationSeconds is >= 0 ? input.CompletedDurationSeconds : null,
                DistanceKm = input.DistanceKm is >= 0f ? input.DistanceKm : null,
                AverageHeartRateBpm = input.AverageHeartRateBpm is >= 0 ? input.AverageHeartRateBpm : null,
                PaceSecondsPerKm = input.PaceSecondsPerKm is >= 0 ? input.PaceSecondsPerKm : null,
                PowerWatts = input.PowerWatts is >= 0 ? input.PowerWatts : null,
                CadenceRpm = input.CadenceRpm is >= 0 ? input.CadenceRpm : null,
                EffortRating = input.EffortRating is >= 1 and <= 10 ? input.EffortRating : null,
                CreatedAt = createdAt
            });
        }

        await _workoutRepository.MergeWorkoutLogAsync(workoutLog);
    }


    public async Task<IReadOnlyList<ExerciseSwapOption>> GetSwapCandidatesAsync(int userId, int workoutDayExerciseId)
    {
        if (userId <= 0 || workoutDayExerciseId <= 0)
            return [];

        var dayExercise = await _db.WorkoutDayExercises
            .Include(e => e.Exercise)
            .Include(e => e.WorkoutDay)
            .ThenInclude(d => d!.WorkoutPlan)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == workoutDayExerciseId);

        if (dayExercise?.Exercise is null)
            return [];

        if (dayExercise.WorkoutDay?.WorkoutPlan is not null && dayExercise.WorkoutDay.WorkoutPlan.UserId != userId)
            return [];

        var currentPlanningId = ExtractPlanningId(dayExercise.Exercise.Code);
        if (currentPlanningId is null)
            return [];

        await _contributions.LoadAsync();

        var currentContributions = _contributions.GetContributions(currentPlanningId.Value);
        if (currentContributions.Count == 0)
            return [];

        var primaryMuscleId = currentContributions
            .OrderByDescending(c => c.Fraction)
            .First()
            .MuscleGroupId;

        var (equipment, injuryFlags) = await LoadSwapEligibilityAsync(userId);

        var allExercises = await _db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync();

        var muscleGroups = await _db.WorkoutMuscleGroups.AsNoTracking()
            .ToDictionaryAsync(mg => mg.Id, mg => mg.Name);

        var candidates = allExercises
            .Where(e => e.Id != currentPlanningId.Value)
            .Where(e => e.IsBodyweight || (e.Required & equipment) == e.Required)
            .Where(e => !WorkoutInjuryRules.IsContraindicated(e, injuryFlags))
            .Select(e =>
            {
                var contribs = _contributions.GetContributions(e.Id);
                var matchScore = ComputeMuscleMatchScore(currentContributions, contribs, primaryMuscleId);
                return (Exercise: e, MatchScore: matchScore, Contribs: contribs);
            })
            .Where(x => x.MatchScore > 0)
            .OrderByDescending(x => x.MatchScore)
            .Take(8)
            .Select(x => new ExerciseSwapOption
            {
                ExerciseId = x.Exercise.Id,
                Name = x.Exercise.Name,
                MuscleMatch = $"{(int)(x.MatchScore * 100)}% muscle match",
                Equipment = x.Exercise.IsBodyweight
                    ? "Bodyweight"
                    : x.Exercise.Required.ToFriendlyName(),
                PrimaryMuscles = string.Join(", ", x.Contribs
                    .Where(c => c.Fraction >= 0.3)
                    .OrderByDescending(c => c.Fraction)
                    .Take(3)
                    .Select(c => muscleGroups.GetValueOrDefault(c.MuscleGroupId, "")))
            })
            .ToList();

        return candidates;
    }

    public async Task SwapExerciseAsync(int userId, int workoutDayExerciseId, int newExerciseId, bool savePreference)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        var dayExercise = await _db.WorkoutDayExercises
            .Include(e => e.Exercise)
            .Include(e => e.WorkoutDay)
            .ThenInclude(d => d!.WorkoutPlan)
            .FirstOrDefaultAsync(e => e.Id == workoutDayExerciseId);

        if (dayExercise is null)
            throw new InvalidOperationException("Workout exercise was not found.");

        if (dayExercise.WorkoutDay?.WorkoutPlan is not null && dayExercise.WorkoutDay.WorkoutPlan.UserId != userId)
            throw new InvalidOperationException("Workout exercise does not belong to the user.");

        var originalPlanningId = dayExercise.Exercise is not null
            ? ExtractPlanningId(dayExercise.Exercise.Code)
            : null;

        var newExerciseDef = await _db.WorkoutExerciseDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == newExerciseId);

        if (newExerciseDef is null)
            throw new InvalidOperationException($"Exercise definition {newExerciseId} was not found.");

        var (equipment, injuryFlags) = await LoadSwapEligibilityAsync(userId);

        if (WorkoutInjuryRules.IsContraindicated(newExerciseDef, injuryFlags))
            throw new InvalidOperationException(
                $"Exercise '{newExerciseDef.Name}' is contraindicated by active injuries.");

        if (!newExerciseDef.IsBodyweight && (newExerciseDef.Required & equipment) != newExerciseDef.Required)
            throw new InvalidOperationException(
                $"Exercise '{newExerciseDef.Name}' requires equipment not available in current profile.");

        var catalogExercise = await _db.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == $"ENGINE_{newExerciseId}");

        if (catalogExercise is null)
            throw new InvalidOperationException($"Catalog exercise for planning ID {newExerciseId} was not found.");

        dayExercise.ExerciseId = catalogExercise.Id;
        await _db.SaveChangesAsync();

        if (savePreference && originalPlanningId is not null)
        {
            var existing = await _db.UserExercisePreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.OriginalExerciseId == originalPlanningId.Value);

            if (existing is not null)
            {
                existing.PreferredExerciseId = newExerciseId;
                existing.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.UserExercisePreferences.Add(new UserExercisePreference
                {
                    UserId = userId,
                    OriginalExerciseId = originalPlanningId.Value,
                    PreferredExerciseId = newExerciseId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
        }
    }

    public Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleAsync(
        int userId,
        DateTime startDate,
        int weeksCount = 4,
        CyclePhase? phase = null)
    {
        return _workoutScheduleService.GetDatedScheduleAsync(userId, startDate, weeksCount, phase);
    }

    public Task RescheduleWorkoutAsync(
        int userId,
        DateTime sourceDate,
        DateTime targetDate)
    {
        return _workoutScheduleService.RescheduleWorkoutAsync(userId, sourceDate, targetDate);
    }

    public Task RescheduleWorkoutDayAsync(
        int userId,
        int workoutDayId,
        DateTime targetDate)
    {
        return _workoutScheduleService.RescheduleWorkoutDayAsync(userId, workoutDayId, targetDate);
    }

    public async Task<string?> GetMovementNotesAsync(int userId, int exerciseId)
    {
        if (userId <= 0 || exerciseId <= 0)
            return null;

        var note = await _db.UserMovementNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == exerciseId)
            .ConfigureAwait(false);

        return note?.Notes;
    }

    public async Task SetMovementNotesAsync(int userId, int exerciseId, string? notes)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (exerciseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(exerciseId), "Exercise ID must be positive.");

        var existing = await _db.UserMovementNotes
            .FirstOrDefaultAsync(n => n.UserId == userId && n.ExerciseId == exerciseId)
            .ConfigureAwait(false);

        await ApplyMovementNoteUpdateAsync(userId, existing, notes, async () =>
        {
            var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(e => e.Id == exerciseId)
                .ConfigureAwait(false);
            return (exerciseId, exercise?.Name ?? string.Empty);
        });
    }

    public async Task<string?> GetMovementNotesByNameAsync(int userId, string exerciseName)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(exerciseName))
            return null;

        var trimmed = exerciseName.Trim();
        var note = await _db.UserMovementNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n =>
                n.UserId == userId && (n.ExerciseName == trimmed || EF.Functions.Like(n.ExerciseName, trimmed)))
            .ConfigureAwait(false);

        return note?.Notes;
    }

    public async Task SetMovementNotesByNameAsync(int userId, string exerciseName, string? notes)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (string.IsNullOrWhiteSpace(exerciseName))
            throw new ArgumentException("Exercise name cannot be empty.", nameof(exerciseName));

        var trimmedName = exerciseName.Trim();
        var existing = await _db.UserMovementNotes
            .FirstOrDefaultAsync(n =>
                n.UserId == userId && (n.ExerciseName == trimmedName || EF.Functions.Like(n.ExerciseName, trimmedName)))
            .ConfigureAwait(false);

        await ApplyMovementNoteUpdateAsync(userId, existing, notes, async () =>
        {
            var exercise = await _db.Exercises.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Name == trimmedName || EF.Functions.Like(e.Name, trimmedName))
                .ConfigureAwait(false);
            return (exercise?.Id ?? 0, exercise?.Name ?? trimmedName);
        });
    }

    public async Task<string?> GetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId)
    {
        if (userId <= 0 || workoutDayExerciseId <= 0)
            return null;

        var wde = await GetUserWorkoutDayExerciseAsync(userId, workoutDayExerciseId).ConfigureAwait(false);
        if (wde is null)
            return null;

        var note = await _db.UserMovementNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.UserId == userId &&
                                      (n.ExerciseId == wde.ExerciseId ||
                                       (wde.Exercise != null && n.ExerciseName == wde.Exercise.Name)))
            .ConfigureAwait(false);

        return note?.Notes;
    }

    public async Task SetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId, string? notes)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (workoutDayExerciseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId),
                "Workout day exercise ID must be positive.");

        var wde = await GetUserWorkoutDayExerciseAsync(userId, workoutDayExerciseId).ConfigureAwait(false);
        if (wde is null)
            throw new KeyNotFoundException("Workout exercise was not found or does not belong to the user.");

        await SetMovementNotesAsync(userId, wde.ExerciseId, notes).ConfigureAwait(false);
    }

    private static WorkoutExerciseItem BuildExerciseItem(
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType,
        WorkoutExerciseLog? previousLog,
        WorkoutExerciseLog? currentLog,
        string? userMovementNotes = null)
    {
        return WorkoutExercisePresentationMapper.BuildExerciseItem(dayExercise, workoutType, previousLog, currentLog,
            userMovementNotes);
    }

    private static bool IsExerciseLogComplete(
        WorkoutExerciseLog? log,
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType)
    {
        return WorkoutExercisePresentationMapper.IsExerciseLogComplete(log, dayExercise, workoutType);
    }

    private static void ValidateExerciseLogInput(
        WorkoutExerciseLogInput input,
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType)
    {
        var metricProfile = WorkoutExercisePresentationMapper.BuildMetricProfile(dayExercise, workoutType);
        if (metricProfile.UsesEnduranceMetrics)
            return;

        var exerciseName = dayExercise.Exercise?.Name ?? "this exercise";
        if (input.CompletedSets <= 0 || input.CompletedReps <= 0)
            throw new InvalidOperationException($"Add sets and reps for {exerciseName} before logging it.");

        if (input.WeightKg is null or < 0f)
            throw new InvalidOperationException(
                $"Add kg for {exerciseName} before logging it. Use 0 kg for bodyweight.");
    }

    private static IReadOnlyList<WorkoutActivitySectionItem> BuildActivitySections(
        WorkoutDay day,
        IReadOnlyList<WorkoutExerciseItem> exerciseItems)
    {
        var itemsByTag = exerciseItems
            .GroupBy(exercise => exercise.ActivityTag)
            .ToDictionary(group => group.Key, group => group.ToList());

        var tags = WorkoutActivityClassifier.BuildActivityTags(day)
            .Where(tag => itemsByTag.ContainsKey(tag))
            .ToList();
        if (tags.Count == 0 && exerciseItems.Count > 0)
            tags.Add(WorkoutActivityClassifier.BuildPrimaryTag(day));

        return tags
            .Select((tag, index) =>
            {
                var sectionExercises = itemsByTag.GetValueOrDefault(tag) ?? [];
                return new WorkoutActivitySectionItem
                {
                    OrderIndex = index + 1,
                    TotalActivities = tags.Count,
                    Tag = tag,
                    Title = WorkoutActivityClassifier.BuildSectionTitle(tag),
                    Subtitle = WorkoutActivityClassifier.BuildSectionSubtitle(tag),
                    MetricText = BuildActivityMetricText(tag, sectionExercises),
                    SummaryText = BuildActivitySectionSummary(sectionExercises),
                    ActivityBackground = WorkoutActivityClassifier.GetBackground(tag),
                    ActivityTextColor = WorkoutActivityClassifier.GetTextColor(tag),
                    Exercises = new ObservableCollection<WorkoutExerciseItem>(sectionExercises)
                };
            })
            .ToList();
    }

    private static string BuildActivityMetricText(
        string activityTag,
        IReadOnlyCollection<WorkoutExerciseItem> exercises)
    {
        if (exercises.Count == 0)
            return "Ready to log";

        if (activityTag == WorkoutActivityClassifier.StrengthTag)
            return $"{exercises.Count} {(exercises.Count == 1 ? "exercise" : "exercises")}";

        var totalSeconds = exercises
            .Select(exercise => ParseMinutesToSeconds(exercise.LoggedDurationMinutesText))
            .Where(seconds => seconds > 0)
            .Sum();
        var durationText = totalSeconds > 0 ? FormatDuration(totalSeconds) : string.Empty;

        if (activityTag == WorkoutActivityClassifier.CardioTag)
        {
            var leadExercise = exercises.FirstOrDefault();
            var cardioType = BuildCardioMetricName(leadExercise?.Name ?? string.Empty);
            return string.IsNullOrWhiteSpace(durationText)
                ? cardioType
                : $"{cardioType} - {durationText}";
        }

        if (activityTag == WorkoutActivityClassifier.RecoveryTag)
            return string.IsNullOrWhiteSpace(durationText)
                ? "Easy recovery"
                : $"{durationText} easy recovery";

        return $"{exercises.Count} {(exercises.Count == 1 ? "movement" : "movements")}";
    }

    private static string BuildActivitySectionSummary(IReadOnlyCollection<WorkoutExerciseItem> exercises)
    {
        var loggedCount = exercises.Count(exercise => exercise.IsLogged);
        return loggedCount == exercises.Count
            ? "Everything here is logged."
            : $"{loggedCount} of {exercises.Count} logged";
    }

    private static string BuildCardioMetricName(string name)
    {
        if (ContainsAny(name, "cycle", "cycling", "ride", "bike"))
            return "Power, cadence, HR";

        if (ContainsAny(name, "swim", "pool"))
            return "Pace / 100m, distance, HR";

        if (ContainsAny(name, "run", "jog", "sprint", "tempo"))
            return "Pace, distance, HR";

        if (ContainsAny(name, "hiit", "interval"))
            return "Intervals, HR, effort";

        return "Duration, distance, HR";
    }

    private static int ParseMinutesToSeconds(string value)
    {
        if (!float.TryParse(value, out var minutes))
            return 0;

        return (int)Math.Round(Math.Max(0f, minutes) * 60f);
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 90)
            return $"{seconds} sec";

        var minutes = (int)Math.Ceiling(seconds / 60d);
        return $"{minutes} min";
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildWorkoutDaySubtitle(WorkoutDay day)
    {
        if (day.WorkoutType is WorkoutType.Rest)
            return "Pure rest day";

        if (HasWorkoutFocus(day, "Rock Climbing", "Climb"))
            return "Climbing volume with pull strength and trunk control.";

        if (HasWorkoutFocus(day, "Yoga"))
            return "Mobility, breath, and control without forcing intensity.";

        if (day.WorkoutType is WorkoutType.Recovery)
            return "Easy movement to improve circulation and leave you fresher.";

        if (day.WorkoutType is WorkoutType.Cardio)
        {
            if (HasWorkoutFocus(day, "HIIT", "Interval", "Sprint"))
                return "Short conditioning blocks with recovery kept honest.";

            if (HasWorkoutFocus(day, "Ride", "Zone 2", "Cycling"))
                return "Steady aerobic work at a repeatable, conversational pace.";

            if (HasWorkoutFocus(day, "Run", "Jog"))
                return "Pace work with enough control to recover and repeat.";

            if (HasWorkoutFocus(day, "Swimming"))
                return "Pool conditioning with relaxed shoulders and clean breathing.";

            return "Conditioning work paced so you can actually recover from it.";
        }

        if (HasWorkoutFocus(day, "Leg", "Squat", "Lunge", "Glute", "Hip Thrust"))
            return "Legs and glutes first, with core work to keep the lift honest.";

        if (HasWorkoutFocus(day, "Upper", "Row", "Pulldown", "Press", "Face Pull"))
            return "Push and pull strength with enough shoulder balance.";

        return "Strength work with clear sets, reps, and room to progress.";
    }

    private static bool HasWorkoutFocus(WorkoutDay day, params string[] terms)
    {
        return ContainsAny(day.Name, terms) ||
               day.WorkoutDayExercises.Any(exercise =>
                   exercise.Exercise?.Name is { } name && ContainsAny(name, terms));
    }

    private static string BuildWorkoutDaySummary(
        WorkoutDay day,
        IReadOnlyCollection<WorkoutDayExercise> exercises)
    {
        var exerciseCount = exercises.Count;
        var seconds = exercises.Sum(exercise => exercise.DurationSeconds ?? 0);
        var hasStrengthWork = exercises.Any(exercise => exercise.Sets > 0 && exercise.Reps > 0);
        if (seconds > 0 && hasStrengthWork)
        {
            var minutes = WorkoutDurationEstimator.EstimateStrengthMinutes(exercises) +
                          (int)Math.Ceiling(seconds / 60d);
            return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}, about {minutes} min";
        }

        if (seconds > 0)
            return
                $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")} with {Math.Ceiling(seconds / 60d):0} min";

        if (day.WorkoutType is WorkoutType.Strength && exerciseCount > 0)
            return
                $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}, about {WorkoutDurationEstimator.EstimateStrengthMinutes(exercises)} min";

        return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}";
    }

    private static int CalculateCompletionPercent(int loggedExerciseCount, int plannedExerciseCount)
    {
        if (plannedExerciseCount <= 0)
            return 0;

        return Math.Clamp((int)Math.Round(loggedExerciseCount * 100d / plannedExerciseCount), 0, 100);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (int)date.Date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.Date.AddDays(-diff);
    }

    private async Task<(EquipmentSet Equipment, InjuryFlag InjuryFlags)> LoadSwapEligibilityAsync(int userId)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        var equipment = ExercisePickerService.MapEquipment(
            profile is not null && Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var parsed)
                ? parsed
                : Equipment.FullGym);

        var injuryLogs = await _injuryRepository.GetActiveAsync(userId);
        var injuries = AdaptiveProfileMapper.FromUserProfile(
            profile ?? new UserProfile(), injuryLogs);
        var injuryFlags = WorkoutInjuryRules.ToFlags(injuries.Injuries);

        return (equipment, injuryFlags);
    }

    private async Task ApplyMovementNoteUpdateAsync(
        int userId,
        UserMovementNote? existing,
        string? notes,
        Func<Task<(int ExerciseId, string ExerciseName)>> resolveNewExerciseInfoAsync)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            if (existing is not null)
            {
                _db.UserMovementNotes.Remove(existing);
                await _db.SaveChangesAsync().ConfigureAwait(false);
            }

            return;
        }

        if (trimmed.Length > 2000)
            throw new ArgumentException("Movement notes cannot exceed 2000 characters.", nameof(notes));

        if (existing is not null)
        {
            existing.Notes = trimmed;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var info = await resolveNewExerciseInfoAsync().ConfigureAwait(false);
            var newNote = new UserMovementNote
            {
                UserId = userId,
                ExerciseId = info.ExerciseId,
                ExerciseName = info.ExerciseName,
                Notes = trimmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _db.UserMovementNotes.AddAsync(newNote).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<WorkoutDayExercise?> GetUserWorkoutDayExerciseAsync(int userId, int workoutDayExerciseId)
    {
        var wde = await _db.WorkoutDayExercises
            .Include(e => e.WorkoutDay)
            .ThenInclude(d => d!.WorkoutPlan)
            .Include(e => e.Exercise)
            .FirstOrDefaultAsync(e => e.Id == workoutDayExerciseId)
            .ConfigureAwait(false);

        if (wde is null || wde.WorkoutDay?.WorkoutPlan?.UserId != userId)
            return null;

        return wde;
    }

    private static int? ExtractPlanningId(string? exerciseCode)
    {
        if (exerciseCode is not null
            && exerciseCode.StartsWith("ENGINE_", StringComparison.Ordinal)
            && int.TryParse(exerciseCode.AsSpan(7), out var id))
            return id;
        return null;
    }

    private static double ComputeMuscleMatchScore(
        IReadOnlyList<ExerciseMuscleContribution> original,
        IReadOnlyList<ExerciseMuscleContribution> candidate,
        int primaryMuscleId)
    {
        if (candidate.Count == 0)
            return 0;

        var candidatePrimary = candidate
            .FirstOrDefault(c => c.MuscleGroupId == primaryMuscleId);

        if (candidatePrimary is null || candidatePrimary.Fraction < 0.2)
            return 0;

        var originalMap = original.ToDictionary(c => c.MuscleGroupId, c => c.Fraction);
        var candidateMap = candidate.ToDictionary(c => c.MuscleGroupId, c => c.Fraction);

        var allMuscles = originalMap.Keys.Union(candidateMap.Keys).ToList();
        if (allMuscles.Count == 0)
            return 0;

        double totalDifference = 0;
        foreach (var muscleId in allMuscles)
        {
            var origFraction = originalMap.GetValueOrDefault(muscleId, 0);
            var candFraction = candidateMap.GetValueOrDefault(muscleId, 0);
            totalDifference += Math.Abs(origFraction - candFraction);
        }

        return Math.Max(0, 1.0 - totalDifference / 2.0);
    }
}
