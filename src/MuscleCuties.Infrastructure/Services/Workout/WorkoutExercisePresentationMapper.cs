using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout;

internal static class WorkoutExercisePresentationMapper
{
    public static WorkoutExerciseItem BuildExerciseItem(
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType,
        WorkoutExerciseLog? previousLog,
        WorkoutExerciseLog? currentLog,
        string? userMovementNotes = null)
    {
        var metricProfile = BuildMetricProfile(dayExercise, workoutType);
        var exercise = dayExercise.Exercise;
        var purpose = BuildExercisePurpose(exercise, workoutType);
        var activityTag = WorkoutActivityClassifier.ClassifyExerciseTag(dayExercise, workoutType);
        var item = new WorkoutExerciseItem
        {
            WorkoutDayExerciseId = dayExercise.Id,
            ExerciseId = dayExercise.ExerciseId,
            MovementNotes = userMovementNotes ?? string.Empty,
            ActivityTag = activityTag,
            ActivityTitle = WorkoutActivityClassifier.BuildSectionTitle(activityTag),
            ActivityBackground = WorkoutActivityClassifier.GetBackground(activityTag),
            ActivityTextColor = WorkoutActivityClassifier.GetTextColor(activityTag),
            IsLogged = IsExerciseLogComplete(currentLog, dayExercise, workoutType),
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
            DurationLabel = metricProfile.UsesTimeUnderTension ? "Minutes under tension" : "Minutes",
            DistanceLabel = metricProfile.UsesSwimmingMetrics ? "Meters" : "km",
            PaceLabel = metricProfile.UsesSwimmingMetrics ? "Pace / 100m" : "Pace / km",
            LoggedSetsText = currentLog is not null ? currentLog.CompletedSets.ToString() :
                dayExercise.Sets > 0 ? dayExercise.Sets.ToString() : string.Empty,
            LoggedRepsText = currentLog is not null ? currentLog.CompletedReps.ToString() :
                dayExercise.Reps > 0 ? dayExercise.Reps.ToString() : string.Empty,
            LoggedWeightText = currentLog?.WeightKg is >= 0f
                ? $"{currentLog.WeightKg.Value:0.#}"
                : BuildSuggestedWeightText(dayExercise, previousLog, metricProfile),
            LoggedDurationMinutesText = currentLog?.CompletedDurationSeconds is >= 0
                ? $"{Math.Ceiling(currentLog.CompletedDurationSeconds.Value / 60d):0}"
                : BuildSuggestedDurationMinutesText(dayExercise, previousLog, metricProfile),
            LoggedDistanceKmText = currentLog?.DistanceKm is >= 0f
                ? $"{currentLog.DistanceKm.Value:0.#}"
                : BuildSuggestedDistanceText(previousLog, metricProfile),
            LoggedPaceText = currentLog?.PaceSecondsPerKm is >= 0
                ? FormatPace(currentLog.PaceSecondsPerKm.Value)
                : BuildSuggestedPaceText(previousLog, metricProfile),
            LoggedHeartRateText = currentLog?.AverageHeartRateBpm is >= 0
                ? currentLog.AverageHeartRateBpm.Value.ToString()
                : BuildSuggestedHeartRateText(previousLog, metricProfile),
            LoggedPowerWattsText = currentLog?.PowerWatts is >= 0
                ? currentLog.PowerWatts.Value.ToString()
                : BuildSuggestedPowerText(previousLog, metricProfile),
            LoggedCadenceRpmText = currentLog?.CadenceRpm is >= 0
                ? currentLog.CadenceRpm.Value.ToString()
                : BuildSuggestedCadenceText(previousLog, metricProfile),
            LoggedEffortText = currentLog?.EffortRating is >= 1 and <= 10
                ? currentLog.EffortRating.Value.ToString()
                : BuildSuggestedEffortText(previousLog, metricProfile)
        };

        if (item.UsesStrengthMetrics)
        {
            var setsCount = currentLog?.CompletedSets ?? (dayExercise.Sets > 0 ? dayExercise.Sets : 3);
            item.EnsureSets(setsCount);
        }

        return item;
    }

    public static bool IsExerciseLogComplete(
        WorkoutExerciseLog? log,
        WorkoutDayExercise dayExercise,
        WorkoutType workoutType)
    {
        if (log is null)
            return false;

        var metricProfile = BuildMetricProfile(dayExercise, workoutType);
        if (metricProfile.UsesEnduranceMetrics)
            return !metricProfile.UsesDuration ||
                   log.CompletedDurationSeconds is > 0;

        return log.CompletedSets > 0 &&
               log.CompletedReps > 0 &&
               log.WeightKg is >= 0f;
    }

    public static ExerciseMetricProfile BuildMetricProfile(WorkoutDayExercise exercise, WorkoutType workoutType)
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
            true,
            true,
            isDistanceCardio,
            isRunning || isSwimming,
            isCycling || isRunning || isSwimming || isHiit || IsDistanceRecoveryExercise(name),
            isCycling,
            isCycling,
            isCycling || isRunning || isSwimming || isHiit,
            isTimeUnderTension,
            isSwimming);
    }

    public static bool IsTimeUnderTensionExercise(string name)
    {
        return ContainsAny(name, "plank", "copenhagen");
    }

    public static bool IsCyclingExercise(string name)
    {
        return ContainsAny(name, "bike", "cycle", "cycling", "ride");
    }

    public static bool IsRunningExercise(string name)
    {
        return ContainsAny(name, "run", "jog", "sprint", "tempo");
    }

    public static bool IsSwimmingExercise(string name)
    {
        return ContainsAny(name, "swim", "pool");
    }

    public static bool IsHiitExercise(string name)
    {
        return ContainsAny(name, "hiit", "interval") && !IsCyclingExercise(name) && !IsRunningExercise(name);
    }

    public static bool IsDistanceRecoveryExercise(string name)
    {
        return ContainsAny(name, "walk");
    }

    public static bool IsRecoveryDurationExercise(string name)
    {
        return ContainsAny(
            name,
            "yoga",
            "vinyasa",
            "mobility",
            "recovery",
            "breathing",
            "pilates",
            "walk");
    }

    public static string BuildExercisePurpose(Exercise? exercise, WorkoutType workoutType)
    {
        var name = exercise?.Name ?? string.Empty;

        if (ContainsAny(name, "Leg Press", "Leg Extension"))
            return
                "Adds stable quad-focused volume so lower-body sessions can grow without every set becoming a balance drill.";

        if (ContainsAny(name, "Squat", "Lunge", "Step-Up"))
            return
                "Builds usable leg strength through knee and hip control, with enough single-leg work to keep both sides honest.";

        if (ContainsAny(name, "Cable Glute Kickback", "Cable Hip Abduction", "Cable Pull-Through"))
            return "Adds targeted glute volume from angles heavy compounds miss, with lower fatigue cost.";

        if (ContainsAny(name, "Hip Thrust", "Glute Bridge", "Back Extension"))
            return "Targets hip extension and glute strength without needing heavy spinal loading.";

        if (ContainsAny(name, "Romanian Deadlift", "Leg Curl"))
            return
                "Builds hamstrings, glutes, and hinge control so pulling strength progresses without rushing the lower back.";

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

        if (workoutType is WorkoutType.Cardio || ContainsAny(name, "Ride", "Walk", "Run", "Jog", "Sprint", "Cycling",
                "Intervals", "Swimming", "HIIT", "Dance"))
            return "Builds conditioning with a pace you can log, repeat, and improve without guessing.";

        return string.IsNullOrWhiteSpace(exercise?.Description)
            ? "Supports today's workout focus with clear, trackable work."
            : exercise.Description;
    }

    public static string BuildTechniqueNotes(Exercise? exercise, ExerciseMetricProfile metricProfile)
    {
        if (!string.IsNullOrWhiteSpace(exercise?.TechniqueNotes))
            return exercise.TechniqueNotes!;

        return WorkoutExerciseGuidance.BuildTechniqueNotes(exercise?.Name, metricProfile.UsesEnduranceMetrics);
    }

    public static string BuildQuickTips(Exercise? exercise, ExerciseMetricProfile metricProfile)
    {
        return WorkoutExerciseGuidance.BuildQuickTips(exercise?.Name, metricProfile.UsesEnduranceMetrics);
    }

    public static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildTargetText(WorkoutDayExercise exercise, ExerciseMetricProfile metricProfile)
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

    public static string BuildPreviousText(WorkoutExerciseLog? previousLog, ExerciseMetricProfile metricProfile)
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

    public static string BuildPreviousEnduranceText(
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

    public static string BuildRecommendationText(
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

    public static string BuildSuggestedWeightText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (metricProfile.UsesEnduranceMetrics || previousLog?.WeightKg is not > 0f)
            return string.Empty;

        return $"{CalculateSuggestedWeight(exercise, previousLog):0.#}";
    }

    public static string BuildSuggestedDurationMinutesText(
        WorkoutDayExercise exercise,
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        if (!metricProfile.UsesDuration)
            return string.Empty;

        var seconds = previousLog?.CompletedDurationSeconds ?? exercise.DurationSeconds;
        return seconds is > 0 ? $"{Math.Ceiling(seconds.Value / 60d):0}" : string.Empty;
    }

    public static string BuildSuggestedDistanceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesDistance && previousLog?.DistanceKm is > 0f
            ? $"{previousLog.DistanceKm.Value:0.#}"
            : string.Empty;
    }

    public static string BuildSuggestedPaceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesPace && previousLog?.PaceSecondsPerKm is > 0
            ? FormatPace(previousLog.PaceSecondsPerKm.Value)
            : string.Empty;
    }

    public static string BuildSuggestedHeartRateText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesHeartRate && previousLog?.AverageHeartRateBpm is > 0
            ? previousLog.AverageHeartRateBpm.Value.ToString()
            : string.Empty;
    }

    public static string BuildSuggestedPowerText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesPower && previousLog?.PowerWatts is > 0
            ? previousLog.PowerWatts.Value.ToString()
            : string.Empty;
    }

    public static string BuildSuggestedCadenceText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesCadence && previousLog?.CadenceRpm is > 0
            ? previousLog.CadenceRpm.Value.ToString()
            : string.Empty;
    }

    public static string BuildSuggestedEffortText(
        WorkoutExerciseLog? previousLog,
        ExerciseMetricProfile metricProfile)
    {
        return metricProfile.UsesEffort && previousLog?.EffortRating is >= 1 and <= 10
            ? previousLog.EffortRating.Value.ToString()
            : string.Empty;
    }

    public static string FormatPace(int paceSecondsPerKm)
    {
        return $"{paceSecondsPerKm / 60}:{paceSecondsPerKm % 60:00}";
    }

    public static string FormatDuration(int seconds)
    {
        if (seconds < 60)
            return $"{seconds}s";

        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        return remainingSeconds > 0 ? $"{minutes}m {remainingSeconds}s" : $"{minutes}m";
    }

    public static float CalculateSuggestedWeight(
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

    internal sealed record ExerciseMetricProfile(
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
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false);
    }
}
