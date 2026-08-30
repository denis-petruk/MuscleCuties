using System.Collections.ObjectModel;
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
        var currentLog = await _workoutRepository.GetWorkoutLogForDayAsync(userId, day.Id, DateTime.Today);
        var currentLogByDayExerciseId = (currentLog?.ExerciseLogs ?? [])
            .GroupBy(log => log.WorkoutDayExerciseId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(log => log.CreatedAt).First());
        var previousLogs = await _workoutRepository.GetExerciseLogsByExerciseIdsAsync(userId, exerciseIds);
        var latestLogByExercise = previousLogs
            .Where(log => currentLog is null || log.WorkoutLogId != currentLog.Id)
            .GroupBy(log => log.ExerciseId)
            .ToDictionary(group => group.Key, group => group.First());

        var exerciseItems = orderedExercises
            .Select(exercise => BuildExerciseItem(
                exercise,
                day.WorkoutType,
                latestLogByExercise.GetValueOrDefault(exercise.ExerciseId),
                currentLogByDayExerciseId.GetValueOrDefault(exercise.Id)))
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
            .Select(log => log.WorkoutDayExerciseId)
            .ToHashSet() ?? [];
        foreach (var input in validExerciseLogs)
            alreadyLoggedIds.Add(input.WorkoutDayExerciseId);

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
                CompletedDurationSeconds = input.CompletedDurationSeconds is >= 0 ? input.CompletedDurationSeconds : null,
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
        WorkoutExerciseLog? previousLog,
        WorkoutExerciseLog? currentLog)
    {
        var metricProfile = BuildMetricProfile(dayExercise, workoutType);
        var exercise = dayExercise.Exercise;
        var purpose = BuildExercisePurpose(exercise, workoutType);
        var activityTag = WorkoutActivityClassifier.ClassifyExerciseTag(dayExercise, workoutType);
        return new WorkoutExerciseItem
        {
            WorkoutDayExerciseId = dayExercise.Id,
            ExerciseId = dayExercise.ExerciseId,
            ActivityTag = activityTag,
            ActivityTitle = WorkoutActivityClassifier.BuildSectionTitle(activityTag),
            ActivityBackground = WorkoutActivityClassifier.GetBackground(activityTag),
            ActivityTextColor = WorkoutActivityClassifier.GetTextColor(activityTag),
            IsLogged = currentLog is not null,
            Name = exercise?.Name ?? "Exercise",
            Description = purpose,
            ImageUrl = exercise?.ImageUrl ?? string.Empty,
            VideoUrl = exercise?.VideoUrl ?? string.Empty,
            TechniqueNotes = BuildTechniqueNotes(exercise, metricProfile),
            QuickTipsText = BuildQuickTips(exercise, metricProfile),
            TargetText = BuildTargetText(dayExercise, metricProfile),
            PreviousText = BuildPreviousText(previousLog, metricProfile),
            RecommendationText = BuildRecommendationText(dayExercise, previousLog, metricProfile),
            UsesEnduranceMetrics = metricProfile.UsesEnduranceMetrics,
            UsesDurationMetric = metricProfile.UsesDuration,
            UsesDistanceMetric = metricProfile.UsesDistance,
            UsesPaceMetric = metricProfile.UsesPace,
            UsesHeartRateMetric = metricProfile.UsesHeartRate,
            UsesPowerMetric = metricProfile.UsesPower,
            UsesCadenceMetric = metricProfile.UsesCadence,
            UsesEffortMetric = metricProfile.UsesEffort,
            UsesWeight = !metricProfile.UsesEnduranceMetrics,
            DurationLabel = metricProfile.UsesTimeUnderTension ? "MINUTES UNDER TENSION" : "MINUTES",
            DistanceLabel = metricProfile.UsesSwimmingMetrics ? "METERS" : "KM",
            PaceLabel = metricProfile.UsesSwimmingMetrics ? "PACE / 100M" : "PACE / KM",
            LoggedSetsText = currentLog is not null ? currentLog.CompletedSets.ToString() : dayExercise.Sets > 0 ? dayExercise.Sets.ToString() : string.Empty,
            LoggedRepsText = currentLog is not null ? currentLog.CompletedReps.ToString() : dayExercise.Reps > 0 ? dayExercise.Reps.ToString() : string.Empty,
            LoggedWeightText = currentLog?.WeightKg is >= 0f ? $"{currentLog.WeightKg.Value:0.#}" : BuildSuggestedWeightText(dayExercise, previousLog, metricProfile),
            LoggedDurationMinutesText = currentLog?.CompletedDurationSeconds is >= 0
                ? $"{Math.Ceiling(currentLog.CompletedDurationSeconds.Value / 60d):0}"
                : BuildSuggestedDurationMinutesText(dayExercise, previousLog, metricProfile),
            LoggedDistanceKmText = currentLog?.DistanceKm is >= 0f ? $"{currentLog.DistanceKm.Value:0.#}" : BuildSuggestedDistanceText(previousLog, metricProfile),
            LoggedPaceText = currentLog?.PaceSecondsPerKm is >= 0 ? FormatPace(currentLog.PaceSecondsPerKm.Value) : BuildSuggestedPaceText(previousLog, metricProfile),
            LoggedHeartRateText = currentLog?.AverageHeartRateBpm is >= 0 ? currentLog.AverageHeartRateBpm.Value.ToString() : BuildSuggestedHeartRateText(previousLog, metricProfile),
            LoggedPowerWattsText = currentLog?.PowerWatts is >= 0 ? currentLog.PowerWatts.Value.ToString() : BuildSuggestedPowerText(previousLog, metricProfile),
            LoggedCadenceRpmText = currentLog?.CadenceRpm is >= 0 ? currentLog.CadenceRpm.Value.ToString() : BuildSuggestedCadenceText(previousLog, metricProfile),
            LoggedEffortText = currentLog?.EffortRating is >= 1 and <= 10 ? currentLog.EffortRating.Value.ToString() : BuildSuggestedEffortText(previousLog, metricProfile)
        };
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

    private static ExerciseMetricProfile BuildMetricProfile(WorkoutDayExercise exercise, WorkoutType workoutType)
    {
        var name = exercise.Exercise?.Name ?? string.Empty;
        var isTimed = exercise.DurationSeconds is > 0;
        var isTimeUnderTension = IsTimeUnderTensionExercise(name);
        var isCycling = IsCyclingExercise(name);
        var isRunning = IsRunningExercise(name);
        var isSwimming = IsSwimmingExercise(name);
        var isHiit = IsHiitExercise(name);
        var isDistanceCardio = isCycling || isRunning || isSwimming || IsDistanceRecoveryExercise(name);
        var usesEndurance = isTimed ||
                             isTimeUnderTension ||
                             IsRecoveryDurationExercise(name) ||
                             isCycling ||
                             isRunning ||
                             isSwimming ||
                             isHiit;

        if (!usesEndurance)
            return ExerciseMetricProfile.Strength;

        return new ExerciseMetricProfile(
            UsesEnduranceMetrics: true,
            UsesDuration: true,
            UsesDistance: isDistanceCardio,
            UsesPace: isRunning || isSwimming,
            UsesHeartRate: isCycling || isRunning || isSwimming || isHiit || IsDistanceRecoveryExercise(name),
            UsesPower: isCycling,
            UsesCadence: isCycling,
            UsesEffort: isCycling || isRunning || isSwimming || isHiit,
            UsesTimeUnderTension: isTimeUnderTension,
            UsesSwimmingMetrics: isSwimming);
    }

    private static bool IsTimeUnderTensionExercise(string name) =>
        ContainsAny(name, "plank", "copenhagen");

    private static bool IsCyclingExercise(string name) =>
        ContainsAny(name, "bike", "cycle", "cycling", "ride");

    private static bool IsRunningExercise(string name) =>
        ContainsAny(name, "run", "jog", "sprint", "tempo");

    private static bool IsSwimmingExercise(string name) =>
        ContainsAny(name, "swim", "pool");

    private static bool IsHiitExercise(string name) =>
        ContainsAny(name, "hiit", "interval") && !IsCyclingExercise(name) && !IsRunningExercise(name);

    private static bool IsDistanceRecoveryExercise(string name) =>
        ContainsAny(name, "walk");

    private static bool IsRecoveryDurationExercise(string name) =>
        ContainsAny(
            name,
            "yoga",
            "vinyasa",
            "mobility",
            "recovery",
            "breathing",
            "pilates",
            "walk");

    private static string BuildExercisePurpose(Exercise? exercise, WorkoutType workoutType)
    {
        var name = exercise?.Name ?? string.Empty;

        if (ContainsAny(name, "Leg Press", "Leg Extension"))
            return "Adds stable quad-focused volume so lower-body sessions can grow without every set becoming a balance drill.";

        if (ContainsAny(name, "Squat", "Lunge", "Step-Up"))
            return "Builds usable leg strength through knee and hip control, with enough single-leg work to keep both sides honest.";

        if (ContainsAny(name, "Cable Glute Kickback", "Cable Hip Abduction", "Cable Pull-Through"))
            return "Adds targeted glute volume from angles heavy compounds miss, with lower fatigue cost.";

        if (ContainsAny(name, "Hip Thrust", "Glute Bridge", "Back Extension"))
            return "Targets hip extension and glute strength without needing heavy spinal loading.";

        if (ContainsAny(name, "Romanian Deadlift", "Leg Curl"))
            return "Builds hamstrings, glutes, and hinge control so pulling strength progresses without rushing the lower back.";

        if (ContainsAny(name, "Push-Up", "Dumbbell Press", "Overhead Press"))
            return "Trains pressing strength while teaching rib position, shoulder control, and stable lockout.";

        if (ContainsAny(name, "Row", "Pulldown", "Pull-Up", "Face Pull", "Climbing"))
            return "Builds pulling strength, upper-back control, grip support, and shoulder balance.";

        if (ContainsAny(name, "Raise", "Curl", "Pressdown", "Rear Delt Fly"))
            return "Adds focused accessory volume where small muscles need clean reps more than heavy loading.";

        if (ContainsAny(name, "Dead Bug", "Plank", "Pallof", "Bird Dog", "Woodchop", "Knee Raise", "Reverse Crunch"))
            return "Trains trunk control so the rest of the workout has better posture, bracing, and transfer.";

        if (ContainsAny(name, "Yoga", "Vinyasa", "Mobility", "Recovery", "Breathing", "Pilates"))
            return "Keeps the session productive with mobility, breathing, and control instead of chasing fatigue.";

        if (workoutType is WorkoutType.Cardio || ContainsAny(name, "Ride", "Walk", "Run", "Jog", "Sprint", "Cycling", "Intervals", "Swimming", "HIIT", "Dance"))
            return "Builds conditioning with a pace you can log, repeat, and improve without guessing.";

        return string.IsNullOrWhiteSpace(exercise?.Description)
            ? "Supports today's workout focus with clear, trackable work."
            : exercise.Description;
    }

    private static string BuildTechniqueNotes(Exercise? exercise, ExerciseMetricProfile metricProfile)
    {
        var name = exercise?.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(exercise?.TechniqueNotes))
            return exercise.TechniqueNotes!;

        if (ContainsAny(name, "Goblet Squat"))
            return "Setup: hold the weight high against your chest, feet about shoulder width, ribs stacked over hips.\nMove: sit between your hips, let knees track with toes, pause if depth gets messy, then drive the floor away.\nFinish: stand tall without leaning back or letting the weight drift forward.";

        if (ContainsAny(name, "Bulgarian Split Squat"))
            return "Setup: front foot far enough forward that the heel stays down, back foot relaxed on the bench, ribs over pelvis.\nMove: lower straight down with a slight forward torso lean, let the front knee track over toes, and keep pressure through the whole front foot.\nFinish: drive up without bouncing off the back leg; stop the set when balance starts stealing the glute/quad work.";

        if (ContainsAny(name, "Walking Lunge"))
            return "Setup: tall posture, eyes forward, steps long enough to keep the front heel planted.\nMove: lower under control, push through the front leg, and let the back knee travel down instead of collapsing inward.\nFinish: step through smoothly and reset your balance before the next rep.";

        if (ContainsAny(name, "Leg Press"))
            return "Setup: feet planted evenly, hips heavy against the pad, knees lined with toes.\nMove: lower until hips stay down and knees track cleanly, then press through mid-foot without locking out hard.\nFinish: control every rep; do not let the sled bounce or pull your pelvis under.";

        if (ContainsAny(name, "Leg Extension"))
            return "Setup: knee joint lined with the machine pivot, pad above the ankle, torso still.\nMove: extend until quads fully squeeze, pause briefly, then lower with control.\nFinish: keep hips down and avoid swinging the first half of the rep.";

        if (ContainsAny(name, "Seated Leg Curl"))
            return "Setup: knees lined with the machine pivot, thigh pad snug, toes relaxed.\nMove: curl through the full range, pause when hamstrings are shortest, then return slowly.\nFinish: keep hips pinned down so the hamstrings do the work.";

        if (ContainsAny(name, "Hip Thrust", "Glute Bridge"))
            return "Setup: upper back supported, chin slightly tucked, feet close enough that shins are near vertical at the top.\nMove: brace first, tuck the pelvis slightly, drive through mid-foot and heels, then squeeze glutes at the top.\nFinish: lower under control without turning it into a lower-back arch.";

        if (ContainsAny(name, "Cable Glute Kickback"))
            return "Setup: cable attached low, torso braced, working hip square to the floor.\nMove: drive the heel back and slightly up without arching the lower back.\nFinish: squeeze the glute, return slowly, and stop before the pelvis rotates open.";

        if (ContainsAny(name, "Cable Hip Abduction"))
            return "Setup: stand tall with the cable on the outside leg, support hand light, pelvis level.\nMove: sweep the leg out from the hip, not the lower back, and keep toes mostly forward.\nFinish: pause where the side glute works hardest, then return under control.";

        if (ContainsAny(name, "Cable Pull-Through"))
            return "Setup: face away from a low cable, soft knees, cable between legs, lats gently tight.\nMove: hinge back until hamstrings load, then drive hips through while keeping ribs down.\nFinish: squeeze glutes without leaning back or turning it into an arm pull.";

        if (ContainsAny(name, "Back Extension"))
            return "Setup: pad below hip crease, chin tucked, ribs down, upper back slightly rounded if you want more glute bias.\nMove: lower until hamstrings stretch, then lift by squeezing glutes and driving hips into the pad.\nFinish: stop at a straight body line; avoid hyperextending the lower back.";

        if (ContainsAny(name, "Romanian Deadlift"))
            return "Setup: soft knees, weight close to thighs, shoulders packed down.\nMove: push hips back like closing a car door, keep the weight close, stop when hamstrings are loaded or back position wants to change.\nFinish: stand by driving hips forward, not by shrugging or leaning back.";

        if (ContainsAny(name, "Step-Up"))
            return "Setup: whole working foot on the box, ribs down, light forward lean.\nMove: push through the working leg until the hip and knee finish together; keep the back foot quiet.\nFinish: step down slowly and reset balance before the next rep.";

        if (ContainsAny(name, "Reverse Lunge"))
            return "Setup: tall posture, feet hip width, eyes forward.\nMove: step back far enough to keep the front heel down, lower with control, and keep the front knee tracking over toes.\nFinish: push the floor away with the front leg and return without bouncing.";

        if (ContainsAny(name, "Incline Push-Up", "Incline Dumbbell Press"))
            return "Setup: shoulder blades back and down, ribs quiet, wrists stacked under the load.\nMove: lower until chest and shoulders stay controlled, then press up while keeping elbows about 30 to 45 degrees from the body.\nFinish: lock out smoothly without shrugging.";

        if (ContainsAny(name, "Overhead Press"))
            return "Setup: squeeze glutes lightly, ribs down, forearms vertical.\nMove: press in a straight path, move your head through after the weight clears the face, and keep wrists over elbows.\nFinish: biceps near ears, shoulders down, no lower-back lean.";

        if (ContainsAny(name, "Assisted Pull-Up"))
            return "Setup: choose assistance that lets reps stay smooth, ribs down, hands just outside shoulders.\nMove: pull elbows down toward ribs while keeping the neck long.\nFinish: lower until shoulders move naturally, then start the next rep without swinging.";

        if (ContainsAny(name, "Dumbbell Row", "Chest Supported Row", "Single-Arm Cable Row", "Seated Cable Row", "Lat Pulldown"))
            return "Setup: brace your trunk before the pull and set shoulders away from ears.\nMove: lead with elbows, pull to ribs or upper chest depending on the exercise, and pause briefly where the back is most active.\nFinish: return slowly until the shoulder blade moves, without losing posture.";

        if (ContainsAny(name, "Rear Delt Fly"))
            return "Setup: hinge or support your chest, soften elbows, shoulders away from ears.\nMove: open the arms wide with rear shoulders, not traps, and stop around shoulder height.\nFinish: pause briefly, then lower slowly without swinging.";

        if (ContainsAny(name, "Face Pull"))
            return "Setup: cable around upper-chest height, thumbs slightly back, ribs stacked.\nMove: pull toward face while elbows travel wide, then rotate gently so hands finish near temples.\nFinish: pause, feel rear shoulders and upper back, then return slowly.";

        if (ContainsAny(name, "Lateral Raise"))
            return "Setup: slight bend in elbows, shoulders relaxed, weight just in front of thighs.\nMove: raise to shoulder height with control, lead with elbows, and stop before traps take over.\nFinish: lower slowly and keep the torso still.";

        if (ContainsAny(name, "Biceps Curl", "Triceps Pressdown"))
            return "Setup: elbows pinned near ribs, shoulders relaxed, wrists neutral.\nMove: use the full range you can control without swinging.\nFinish: pause where the target muscle is loaded and lower with the same tempo.";

        if (ContainsAny(name, "Dead Bug"))
            return "Setup: low back gently heavy on the floor, ribs down, knees over hips.\nMove: reach opposite arm and leg only as far as you can without the back arching.\nFinish: exhale, return slowly, and switch sides with control.";

        if (ContainsAny(name, "Copenhagen Side Plank"))
            return "Setup: top leg supported on a bench, elbow under shoulder, ribs stacked.\nMove: lift hips and pull the lower leg toward the bench without twisting.\nFinish: hold only while hips stay level; regress by bending the top knee if form breaks.";

        if (ContainsAny(name, "Side Plank", "Plank"))
            return "Setup: elbows under shoulders, ribs down, glutes lightly squeezed.\nMove: create a long line from head to heels and breathe behind the brace.\nFinish: stop the set before hips sag or shoulders pinch.";

        if (ContainsAny(name, "Pallof Press"))
            return "Setup: stand tall side-on to the cable or band, knees soft, ribs stacked.\nMove: press straight out and resist rotation instead of twisting back.\nFinish: pause at full reach, breathe, then bring hands back in slowly.";

        if (ContainsAny(name, "Cable Woodchop"))
            return "Setup: set the cable high or low, stand athletic, ribs down before you rotate.\nMove: rotate through the upper back and hips together while the arms guide the handle.\nFinish: control the return and avoid yanking from the lower back.";

        if (ContainsAny(name, "Hanging Knee Raise"))
            return "Setup: hang with shoulders active and ribs slightly down.\nMove: curl knees toward chest by rolling the pelvis, not just swinging the legs.\nFinish: lower slowly until the body is still before the next rep.";

        if (ContainsAny(name, "Reverse Crunch"))
            return "Setup: lie down with ribs heavy and knees bent.\nMove: curl the pelvis up first, then bring knees toward chest without throwing momentum.\nFinish: lower slowly and keep the low back controlled.";

        if (ContainsAny(name, "Bird Dog"))
            return "Setup: hands under shoulders, knees under hips, spine quiet.\nMove: reach opposite arm and leg long without shifting hips.\nFinish: pause, return softly, and keep each rep clean.";

        if (ContainsAny(name, "Rock Climbing", "Climb"))
            return "Setup: warm up with easy routes, open the shoulders, and keep footwork deliberate.\nMove: drive from legs first, keep hips close to the wall, and use arms to position instead of yanking every move.\nFinish: stop before grip turns sloppy; quality routes beat exhausted attempts.";

        if (ContainsAny(name, "HIIT Intervals"))
            return "Setup: warm up until joints feel ready and breathing is awake.\nMove: make the hard blocks powerful, keep the easy blocks truly easy, and stop before form turns messy.\nFinish: cool down for a few minutes and log heart rate, duration, and effort.";

        if (ContainsAny(name, "Bike Intervals", "Cycling Intervals"))
            return "Setup: warm up until cadence feels smooth and breathing is ready.\nMove: push the hard blocks with strong cadence, then make the easy blocks truly easy.\nFinish: cool down until breathing settles and log pace or heart rate honestly.";

        if (ContainsAny(name, "Running Intervals", "Tempo Run", "Easy Run"))
            return "Setup: start with easy jogging and a few relaxed strides.\nMove: keep posture tall, land quietly, and use pace as feedback instead of chasing every split.\nFinish: walk it down, then log distance, pace, and heart rate if available.";

        if (ContainsAny(name, "Zone 2 Ride", "Easy Walk", "Swimming", "Dance Cardio"))
            return "Setup: start easy for the first few minutes.\nMove: stay at a pace where breathing is controlled and repeatable; you should feel like you could continue longer.\nFinish: taper down instead of stopping suddenly, then log time, distance, and heart rate if available.";

        if (ContainsAny(name, "Yoga", "Vinyasa", "Mobility", "Recovery", "Pilates", "Breathing"))
            return "Setup: choose a calm pace and give each position enough time to change how you feel.\nMove: breathe into the tight area, use pain-free range, and keep transitions smooth.\nFinish: leave the session feeling clearer, not crushed.";

        return metricProfile.UsesEnduranceMetrics
            ? "Setup: start easy, find a sustainable rhythm, and keep effort trackable.\nMove: hold the planned time with clean breathing and steady posture.\nFinish: cool down and log the metric that best describes the session."
            : "Setup: brace first and own the starting position.\nMove: use the full range you can control, keep tempo smooth, and stop reps when form changes.\nFinish: log the weight only if the target reps were clean.";
    }

    private static string BuildQuickTips(Exercise? exercise, ExerciseMetricProfile metricProfile)
    {
        var name = exercise?.Name ?? string.Empty;

        if (ContainsAny(name, "Yoga", "Vinyasa", "Mobility", "Recovery", "Breathing", "Pilates"))
            return "Keep effort at a 4 to 6 out of 10. Longer holds are fine; sharp pain is not. Good recovery should make the next session easier.";

        if (ContainsAny(name, "Rock Climbing", "Climb"))
            return "Log total climbing time and keep 2 to 3 good attempts in reserve. If grip or elbows feel irritated, switch to easier routes or finish early.";

        if (metricProfile.UsesEnduranceMetrics)
            return "Use the same bike, route, or pool setup when you can. Better data comes from repeatable efforts, not random hero days.";

        if (ContainsAny(name, "Squat", "Lunge", "Step-Up", "Romanian Deadlift", "Hip Thrust", "Dumbbell Press", "Overhead Press", "Row", "Pulldown"))
            return "Aim for 1 to 3 reps in reserve. Add weight next time only when every set hits the target with the same form.";

        return "Make the last rep look like the first. If the target muscle disappears, slow down before adding load.";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

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

    private static bool HasWorkoutFocus(WorkoutDay day, params string[] terms) =>
        ContainsAny(day.Name, terms) ||
        day.WorkoutDayExercises.Any(exercise =>
            exercise.Exercise?.Name is { } name && ContainsAny(name, terms));

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
            return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")} with {Math.Ceiling(seconds / 60d):0} min";

        if (day.WorkoutType is WorkoutType.Strength && exerciseCount > 0)
            return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}, about {WorkoutDurationEstimator.EstimateStrengthMinutes(exercises)} min";

        return $"{exerciseCount} {(exerciseCount == 1 ? "exercise" : "exercises")}";
    }

    private static string BuildTargetText(WorkoutDayExercise exercise, ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesTimeUnderTension)
            return exercise.DurationSeconds is > 0
                ? $"{FormatDuration(exercise.DurationSeconds.Value)} under tension"
                : "Time under tension";

        if (metricProfile.UsesPower)
            return exercise.DurationSeconds is > 0
                ? $"{FormatDuration(exercise.DurationSeconds.Value)} - watts, rpm, HR"
                : "Watts, rpm, HR";

        if (metricProfile.UsesEnduranceMetrics)
            return exercise.DurationSeconds is > 0
                ? $"{FormatDuration(exercise.DurationSeconds.Value)} steady"
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
            parts.Add(metricProfile.UsesTimeUnderTension
                ? $"{FormatDuration(previousLog.CompletedDurationSeconds.Value)} tension"
                : FormatDuration(previousLog.CompletedDurationSeconds.Value));
        if (metricProfile.UsesPower && previousLog.PowerWatts is > 0)
            parts.Add($"{previousLog.PowerWatts.Value} W");
        if (metricProfile.UsesCadence && previousLog.CadenceRpm is > 0)
            parts.Add($"{previousLog.CadenceRpm.Value} rpm");
        if (metricProfile.UsesDistance && previousLog.DistanceKm is > 0f)
            parts.Add(metricProfile.UsesSwimmingMetrics
                ? $"{previousLog.DistanceKm.Value:0.#} m"
                : $"{previousLog.DistanceKm.Value:0.#} km");
        if (metricProfile.UsesPace && previousLog.PaceSecondsPerKm is > 0)
            parts.Add(metricProfile.UsesSwimmingMetrics
                ? $"{FormatPace(previousLog.PaceSecondsPerKm.Value)}/100m"
                : $"{FormatPace(previousLog.PaceSecondsPerKm.Value)}/km");
        if (metricProfile.UsesHeartRate && previousLog.AverageHeartRateBpm is > 0)
            parts.Add($"{previousLog.AverageHeartRateBpm.Value} bpm");
        if (metricProfile.UsesEffort && previousLog.EffortRating is >= 1 and <= 10)
            parts.Add($"effort {previousLog.EffortRating.Value}/10");

        return parts.Count == 0 ? "Logged before" : $"Last {string.Join(" with ", parts)}";
    }

    private static string BuildRecommendationText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesPower)
            return previousLog?.PowerWatts is > 0
                ? $"Start near {previousLog.PowerWatts.Value} W, keep cadence around 80-95 rpm, and let heart rate confirm the effort."
                : "Log watts first if you have them, cadence around 80-95 rpm, plus heart rate and effort.";

        if (metricProfile.UsesPace)
            return previousLog?.PaceSecondsPerKm is > 0
                ? $"Stay near {FormatPace(previousLog.PaceSecondsPerKm.Value)}/km and keep breathing smooth."
                : "Log pace and heart rate so your cardio trend gets smarter.";

        if (metricProfile.UsesTimeUnderTension)
            return "Own every second. Stop before hips drop, shoulders pinch, or breathing disappears.";

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

    private static string BuildSuggestedPowerText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesPower && previousLog?.PowerWatts is > 0
            ? previousLog.PowerWatts.Value.ToString()
            : string.Empty;

    private static string BuildSuggestedCadenceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesCadence && previousLog?.CadenceRpm is > 0
            ? previousLog.CadenceRpm.Value.ToString()
            : string.Empty;

    private static string BuildSuggestedEffortText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile) =>
        metricProfile.UsesEffort && previousLog?.EffortRating is >= 1 and <= 10
            ? previousLog.EffortRating.Value.ToString()
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

    private static DateTime GetWeekStart(DateTime date) =>
        date.Date.AddDays(-(int)date.Date.DayOfWeek);

    private sealed record ExerciseMetricProfile(
        bool UsesEnduranceMetrics,
        bool UsesDuration,
        bool UsesDistance,
        bool UsesPace,
        bool UsesHeartRate,
        bool UsesPower,
        bool UsesCadence,
        bool UsesEffort,
        bool UsesTimeUnderTension,
        bool UsesSwimmingMetrics)
    {
        public static ExerciseMetricProfile Strength { get; } = new(
            UsesEnduranceMetrics: false,
            UsesDuration: false,
            UsesDistance: false,
            UsesPace: false,
            UsesHeartRate: false,
            UsesPower: false,
            UsesCadence: false,
            UsesEffort: false,
            UsesTimeUnderTension: false,
            UsesSwimmingMetrics: false);
    }
}
