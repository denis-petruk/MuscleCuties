using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class WorkoutActivityClassifier
{
    public const string StrengthTag = "STRENGTH";
    public const string CardioTag = "CARDIO";
    public const string RecoveryTag = "RECOVERY";
    public const string RestTag = "REST";

    public static bool IsDarkTheme { get; set; }

    public static string BuildPrimaryTag(WorkoutDay day)
    {
        return day.WorkoutType switch
        {
            WorkoutType.Cardio => CardioTag,
            WorkoutType.Recovery => RecoveryTag,
            WorkoutType.Rest => RestTag,
            _ => StrengthTag
        };
    }

    public static IReadOnlyList<string> BuildActivityTags(WorkoutDay day)
    {
        if (day.WorkoutType is WorkoutType.Rest)
            return [RestTag];

        var tags = new List<string> { BuildPrimaryTag(day) };
        foreach (var exercise in day.WorkoutDayExercises.OrderBy(exercise => exercise.Id))
        {
            var tag = ClassifyExerciseTag(exercise, day.WorkoutType);
            if (!tags.Contains(tag))
                tags.Add(tag);
        }

        return tags.Where(tag => tag != RestTag).ToList();
    }

    public static string ClassifyExerciseTag(WorkoutDayExercise exercise, WorkoutType workoutType)
    {
        if (workoutType is WorkoutType.Rest)
            return RestTag;

        var exerciseName = exercise.Exercise?.Name ?? string.Empty;

        if (IsRecoveryExercise(exerciseName))
            return RecoveryTag;

        if (workoutType is not WorkoutType.Recovery && IsCardioExercise(exerciseName))
            return CardioTag;

        return workoutType switch
        {
            WorkoutType.Cardio => CardioTag,
            WorkoutType.Recovery => RecoveryTag,
            _ => StrengthTag
        };
    }

    public static string BuildSectionTitle(string activityTag)
    {
        return activityTag switch
        {
            CardioTag => "Cardio activity",
            RecoveryTag => "Recovery activity",
            RestTag => "Rest day",
            StrengthTag => "Strength activity",
            _ => "Workout activity"
        };
    }

    public static string BuildSectionSubtitle(string activityTag)
    {
        return activityTag switch
        {
            CardioTag => "Log one cardio piece when you finish it.",
            RecoveryTag => "Log one recovery piece when it is done.",
            RestTag => "Log the rest when you actually took it.",
            StrengthTag => "Log one lift at a time, or finish this strength block together.",
            _ => "Log this block as you complete it."
        };
    }

    public static Color GetBackground(string activityTag) => GetBackground(activityTag, IsDarkTheme);

    public static Color GetBackground(string activityTag, bool isDark)
    {
        if (isDark)
        {
            return activityTag switch
            {
                CardioTag => Color.FromArgb("#1A3A38"),
                RecoveryTag => Color.FromArgb("#1E3520"),
                RestTag => Color.FromArgb("#2E2B26"),
                StrengthTag => Color.FromArgb("#3D2435"),
                _ => Color.FromArgb("#3A2931")
            };
        }

        return activityTag switch
        {
            CardioTag => Color.FromArgb("#E0F2F1"),
            RecoveryTag => Color.FromArgb("#E8F5E9"),
            RestTag => Color.FromArgb("#F0EFEA"),
            StrengthTag => Color.FromArgb("#F8DFF1"),
            _ => Color.FromArgb("#F8EEF4")
        };
    }

    public static Color GetTextColor(string activityTag) => GetTextColor(activityTag, IsDarkTheme);

    public static Color GetTextColor(string activityTag, bool isDark)
    {
        if (isDark)
        {
            return activityTag switch
            {
                CardioTag => Color.FromArgb("#7ECBC5"),
                RecoveryTag => Color.FromArgb("#8FBF8F"),
                RestTag => Color.FromArgb("#B5AFA3"),
                StrengthTag => Color.FromArgb("#D98AAF"),
                _ => Color.FromArgb("#AE8D9B")
            };
        }

        return activityTag switch
        {
            CardioTag => Color.FromArgb("#1F6F68"),
            RecoveryTag => Color.FromArgb("#3A6B3A"),
            RestTag => Color.FromArgb("#5F5A50"),
            StrengthTag => Color.FromArgb("#8D3A5F"),
            _ => Color.FromArgb("#5B4650")
        };
    }

    public static bool IsRecoveryTag(string tag)
    {
        return tag.Contains(RecoveryTag, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRestTag(string tag)
    {
        return tag.Contains(RestTag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoveryExercise(string name)
    {
        return ContainsAny(
            name,
            "yoga",
            "vinyasa",
            "mobility",
            "recovery",
            "breathing",
            "pilates",
            "stretch",
            "cooldown");
    }

    private static bool IsCardioExercise(string name)
    {
        return ContainsAny(
            name,
            "bike",
            "cycle",
            "cycling",
            "ride",
            "run",
            "jog",
            "sprint",
            "swim",
            "cardio",
            "hiit",
            "interval",
            "tempo");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
