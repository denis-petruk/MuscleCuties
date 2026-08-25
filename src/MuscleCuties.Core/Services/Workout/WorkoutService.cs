using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout;

public class WorkoutService : IWorkoutService
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWorkoutPlanner _workoutPlanner;

    public WorkoutService(
        IWorkoutRepository workoutRepository,
        IUserRepository userRepository,
        IWorkoutPlanner workoutPlanner)
    {
        _workoutRepository = workoutRepository;
        _userRepository = userRepository;
        _workoutPlanner = workoutPlanner;
    }

    public async Task<WorkoutPlanSummary> GetPlanSummaryAsync(int userId, CyclePhase phase)
    {
        var activePlan = await EnsureGeneratedPlanAsync(userId, phase);
        if (activePlan is null)
            return new WorkoutPlanSummary(null, [], []);

        var workoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id);
        var workouts = _workoutPlanner.BuildWorkoutItems(workoutDays);

        return new WorkoutPlanSummary(activePlan, workoutDays, workouts);
    }

    public async Task<TodaysWorkoutSummary> GetTodaysSummaryAsync(
        int userId,
        CyclePhase phase,
        DateTime date)
    {
        var activePlan = await EnsureGeneratedPlanAsync(userId, phase);
        if (activePlan is null)
            return TodaysWorkoutSummary.RestDay;

        var workoutDays = await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id);
        var workoutLogs = await _workoutRepository.GetWorkoutLogsByDateAsync(userId, date);

        return _workoutPlanner.BuildTodaysSummary(activePlan, workoutDays, workoutLogs, phase, date);
    }

    public async Task RegenerateActivePlanAsync(int userId, CyclePhase phase)
    {
        await BuildAndReplaceGeneratedPlanAsync(userId, phase);
    }

    public async Task<WorkoutSessionDetail> GetWorkoutSessionDetailAsync(int userId, int workoutDayId)
    {
        var day = await _workoutRepository.GetWorkoutDayWithExercisesAsync(workoutDayId);
        if (day is null)
            throw new InvalidOperationException("Workout session was not found.");

        if (day.WorkoutType is WorkoutType.Rest)
        {
            return new WorkoutSessionDetail(
                day.Id,
                "Living happy life",
                "Pure rest day",
                "No exercises today. Log the rest if you actually took it.",
                [],
                IsRestDay: true);
        }

        var orderedExercises = day.WorkoutDayExercises
            .OrderBy(exercise => exercise.Id)
            .ToList();
        var exerciseIds = orderedExercises
            .Select(exercise => exercise.ExerciseId)
            .Distinct()
            .ToList();
        var previousLogs = await _workoutRepository.GetExerciseLogsByExerciseIdsAsync(userId, exerciseIds);
        var latestLogByExercise = previousLogs
            .GroupBy(log => log.ExerciseId)
            .ToDictionary(group => group.Key, group => group.First());

        var exerciseItems = orderedExercises
            .Select(exercise => BuildExerciseItem(
                exercise,
                day.WorkoutType,
                latestLogByExercise.GetValueOrDefault(exercise.ExerciseId)))
            .ToList();

        return new WorkoutSessionDetail(
            day.Id,
            day.Name,
            BuildWorkoutDaySubtitle(day),
            BuildWorkoutDaySummary(orderedExercises),
            exerciseItems);
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
            await _workoutRepository.AddWorkoutLogAsync(new WorkoutLog
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
            .ToList();

        if (validExerciseLogs.Count == 0)
            throw new InvalidOperationException("Log at least one exercise before saving this workout.");

        var createdAt = DateTime.UtcNow;

        var workoutLog = new WorkoutLog
        {
            UserId = userId,
            WorkoutDayId = day.Id,
            Date = date.Date,
            CompletionPercent = CalculateCompletionPercent(validExerciseLogs.Count, day.WorkoutDayExercises.Count),
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
                CompletedDurationSeconds = input.CompletedDurationSeconds is >= 0 ? input.CompletedDurationSeconds : null,
                DistanceKm = input.DistanceKm is >= 0f ? input.DistanceKm : null,
                AverageHeartRateBpm = input.AverageHeartRateBpm is >= 0 ? input.AverageHeartRateBpm : null,
                PaceSecondsPerKm = input.PaceSecondsPerKm is >= 0 ? input.PaceSecondsPerKm : null,
                CreatedAt = createdAt
            });
        }

        await _workoutRepository.AddWorkoutLogAsync(workoutLog);
    }

    private async Task<WorkoutPlan?> EnsureGeneratedPlanAsync(
        int userId,
        CyclePhase phase)
    {
        var activePlan = await _workoutRepository.GetActivePlanAsync(userId);
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile is null)
            return activePlan;

        List<WorkoutDay> activePlanDays = activePlan is null
            ? []
            : await _workoutRepository.GetWorkoutDaysByPlanAsync(activePlan.Id);
        var snapshot = await _userRepository.GetLatestSnapshotAsync(userId);

        if (!_workoutPlanner.ShouldReplaceGeneratedPlan(activePlan, activePlanDays, profile, snapshot, phase))
            return activePlan;

        return await BuildAndReplaceGeneratedPlanAsync(userId, phase) ?? activePlan;
    }

    private async Task<WorkoutPlan?> BuildAndReplaceGeneratedPlanAsync(int userId, CyclePhase phase)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile is null)
            return null;

        var exercises = await _workoutRepository.GetAllExercisesAsync();
        if (exercises.Count == 0)
            return null;

        var snapshot = await _userRepository.GetLatestSnapshotAsync(userId);
        var generatedPlan = _workoutPlanner.BuildGeneratedPlan(
            userId,
            profile,
            snapshot,
            exercises,
            phase,
            DateTime.UtcNow);

        return await _workoutRepository.ReplaceActivePlanAsync(generatedPlan);
    }

    private static WorkoutExerciseItem BuildExerciseItem(
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType,
        WorkoutExerciseLog? previousLog)
    {
        var metricProfile = BuildMetricProfile(dayExercise, workoutType);
        var exercise = dayExercise.Exercise;
        return new WorkoutExerciseItem
        {
            WorkoutDayExerciseId = dayExercise.Id,
            ExerciseId = dayExercise.ExerciseId,
            Name = exercise?.Name ?? "Exercise",
            Description = exercise?.Description ?? string.Empty,
            ImageUrl = exercise?.ImageUrl ?? string.Empty,
            VideoUrl = exercise?.VideoUrl ?? string.Empty,
            TechniqueNotes = exercise?.TechniqueNotes ?? exercise?.Description ?? string.Empty,
            TargetText = BuildTargetText(dayExercise, metricProfile),
            PreviousText = BuildPreviousText(previousLog, metricProfile),
            RecommendationText = BuildRecommendationText(dayExercise, previousLog, metricProfile),
            UsesEnduranceMetrics = metricProfile.UsesEnduranceMetrics,
            UsesDurationMetric = metricProfile.UsesDuration,
            UsesDistanceMetric = metricProfile.UsesDistance,
            UsesPaceMetric = metricProfile.UsesPace,
            UsesHeartRateMetric = metricProfile.UsesHeartRate,
            UsesWeight = !metricProfile.UsesEnduranceMetrics,
            LoggedSetsText = dayExercise.Sets > 0 ? dayExercise.Sets.ToString() : string.Empty,
            LoggedRepsText = dayExercise.Reps > 0 ? dayExercise.Reps.ToString() : string.Empty,
            LoggedWeightText = BuildSuggestedWeightText(dayExercise, previousLog, metricProfile),
            LoggedDurationMinutesText = BuildSuggestedDurationMinutesText(dayExercise, previousLog, metricProfile),
            LoggedDistanceKmText = BuildSuggestedDistanceText(previousLog, metricProfile),
            LoggedPaceText = BuildSuggestedPaceText(previousLog, metricProfile),
            LoggedHeartRateText = BuildSuggestedHeartRateText(previousLog, metricProfile)
        };
    }

    private static ExerciseMetricProfile BuildMetricProfile(WorkoutDayExercise exercise, WorkoutType workoutType)
    {
        var name = exercise.Exercise?.Name ?? string.Empty;
        var isTimed = exercise.DurationSeconds is > 0;
        var isDistanceCardio = IsDistanceCardioExercise(name);
        var usesEndurance = isTimed ||
                             workoutType is WorkoutType.Cardio or WorkoutType.Recovery;

        if (!usesEndurance)
            return ExerciseMetricProfile.Strength;

        return new ExerciseMetricProfile(
            UsesEnduranceMetrics: true,
            UsesDuration: isTimed || workoutType is WorkoutType.Cardio or WorkoutType.Recovery,
            UsesDistance: isDistanceCardio,
            UsesPace: isDistanceCardio,
            UsesHeartRate: isDistanceCardio);
    }

    private static bool IsDistanceCardioExercise(string name) =>
        ContainsAny(
            name,
            "bike",
            "ride",
            "walk",
            "run",
            "jog",
            "sprint",
            "cardio",
            "interval");

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string BuildWorkoutDaySubtitle(WorkoutDay day) =>
        day.WorkoutType is WorkoutType.Rest ? "Pure rest day" : day.WorkoutType.ToString();

    private static string BuildWorkoutDaySummary(IReadOnlyCollection<WorkoutDayExercise> exercises)
    {
        var exerciseCount = exercises.Count;
        var seconds = exercises.Sum(exercise => exercise.DurationSeconds ?? 0);
        if (seconds > 0)
            return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")} with {Math.Ceiling(seconds / 60d):0} min";

        return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}";
    }

    private static string BuildTargetText(WorkoutDayExercise exercise, ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesEnduranceMetrics)
            return exercise.DurationSeconds is > 0
                ? $"{Math.Ceiling(exercise.DurationSeconds.Value / 60d):0} min steady"
                : "Steady effort";

        if (exercise.Sets > 0 && exercise.Reps > 0)
            return $"{exercise.Sets} sets x {exercise.Reps} reps";

        return "Flexible";
    }

    private static string BuildPreviousText(WorkoutExerciseLog? previousLog, ExerciseMetricProfile metricProfile)
    {
        if (previousLog is null)
            return "No previous log";

        if (metricProfile.UsesEnduranceMetrics)
            return BuildPreviousEnduranceText(previousLog, metricProfile);

        var weightText = previousLog.WeightKg is > 0f
            ? $"{previousLog.WeightKg.Value:0.#} kg"
            : "Bodyweight";
        return $"Last {weightText} with {previousLog.CompletedSets} x {previousLog.CompletedReps}";
    }

    private static string BuildPreviousEnduranceText(
        WorkoutExerciseLog previousLog,
        ExerciseMetricProfile metricProfile)
    {
        var parts = new List<string>();
        if (metricProfile.UsesDuration && previousLog.CompletedDurationSeconds is > 0)
            parts.Add($"{Math.Ceiling(previousLog.CompletedDurationSeconds.Value / 60d):0} min");
        if (metricProfile.UsesDistance && previousLog.DistanceKm is > 0f)
            parts.Add($"{previousLog.DistanceKm.Value:0.#} km");
        if (metricProfile.UsesPace && previousLog.PaceSecondsPerKm is > 0)
            parts.Add($"{FormatPace(previousLog.PaceSecondsPerKm.Value)}/km");
        if (metricProfile.UsesHeartRate && previousLog.AverageHeartRateBpm is > 0)
            parts.Add($"{previousLog.AverageHeartRateBpm.Value} bpm");

        return parts.Count == 0 ? "Logged before" : $"Last {string.Join(" with ", parts)}";
    }

    private static string BuildRecommendationText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesPace)
            return previousLog?.PaceSecondsPerKm is > 0
                ? $"Stay near {FormatPace(previousLog.PaceSecondsPerKm.Value)}/km and keep breathing smooth."
                : "Log pace and heart rate so your cardio trend gets smarter.";

        if (metricProfile.UsesEnduranceMetrics)
            return "Own the full time with clean, controlled form.";

        if (previousLog?.WeightKg is not > 0f)
            return "Pick a steady starting weight.";

        var suggestedWeight = CalculateSuggestedWeight(exercise, previousLog);
        if (suggestedWeight > previousLog.WeightKg.Value)
            return $"Try {suggestedWeight:0.#} kg";

        return $"Repeat {previousLog.WeightKg.Value:0.#} kg";
    }

    private static string BuildSuggestedWeightText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesEnduranceMetrics || previousLog?.WeightKg is not > 0f)
            return string.Empty;

        return $"{CalculateSuggestedWeight(exercise, previousLog):0.#}";
    }

    private static string BuildSuggestedDurationMinutesText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (!metricProfile.UsesDuration)
            return string.Empty;

        var seconds = previousLog?.CompletedDurationSeconds ?? exercise.DurationSeconds;
        return seconds is > 0 ? $"{Math.Ceiling(seconds.Value / 60d):0}" : string.Empty;
    }

    private static string BuildSuggestedDistanceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesDistance && previousLog?.DistanceKm is > 0f
            ? $"{previousLog.DistanceKm.Value:0.#}"
            : string.Empty;

    private static string BuildSuggestedPaceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesPace && previousLog?.PaceSecondsPerKm is > 0
            ? FormatPace(previousLog.PaceSecondsPerKm.Value)
            : string.Empty;

    private static string BuildSuggestedHeartRateText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesHeartRate && previousLog?.AverageHeartRateBpm is > 0
            ? previousLog.AverageHeartRateBpm.Value.ToString()
            : string.Empty;

    private static string FormatPace(int paceSecondsPerKm) =>
        $"{paceSecondsPerKm / 60}:{paceSecondsPerKm % 60:00}";

    private static float CalculateSuggestedWeight(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog previousLog)
    {
        var previousWeight = previousLog.WeightKg ?? 0f;
        var completedTarget =
            previousLog.CompletedSets >= exercise.Sets &&
            previousLog.CompletedReps >= exercise.Reps;
        if (!completedTarget)
            return previousWeight;

        var increment = previousWeight < 20f ? 1f : 2.5f;
        return (float)Math.Round((previousWeight + increment) * 2, MidpointRounding.AwayFromZero) / 2f;
    }

    private static int CalculateCompletionPercent(int loggedExerciseCount, int plannedExerciseCount)
    {
        if (plannedExerciseCount <= 0)
            return 0;

        return Math.Clamp((int)Math.Round(loggedExerciseCount * 100d / plannedExerciseCount), 0, 100);
    }

    private sealed record ExerciseMetricProfile(
        bool UsesEnduranceMetrics,
        bool UsesDuration,
        bool UsesDistance,
        bool UsesPace,
        bool UsesHeartRate)
    {
        public static ExerciseMetricProfile Strength { get; } = new(
            UsesEnduranceMetrics: false,
            UsesDuration: false,
            UsesDistance: false,
            UsesPace: false,
            UsesHeartRate: false);
    }
}
