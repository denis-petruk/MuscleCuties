using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    public IReadOnlyList<WorkoutListItem> BuildWorkoutItems(
        IReadOnlyCollection<WorkoutDay> workoutDays)
    {
        var daysByWeekday = workoutDays
            .GroupBy(day => day.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.First());

        return Enumerable.Range(0, 7)
            .Select(dayOfWeek =>
            {
                if (!daysByWeekday.TryGetValue(dayOfWeek, out var day))
                    return BuildRestDayItem(dayOfWeek);

                if (day.WorkoutType is WorkoutType.Rest)
                    return BuildRestDayItem(day.DayOfWeek, day.Id);

                var activityTag = BuildActivityTag(day);
                return new WorkoutListItem(
                    day.Id,
                    activityTag,
                    FormatDayLabel(day.DayOfWeek),
                    day.Name,
                    BuildDurationText(day),
                    BuildExerciseCountText(day),
                    BuildDetailsText(day),
                    GetActivityBackground(activityTag),
                    GetActivityTextColor(activityTag));
            })
            .ToList();
    }

    public TodaysWorkoutSummary BuildTodaysSummary(
        WorkoutPlan? plan,
        IReadOnlyCollection<WorkoutDay> workoutDays,
        IReadOnlyCollection<WorkoutLog> workoutLogs,
        CyclePhase phase,
        DateTime date)
    {
        if (plan is null)
            return TodaysWorkoutSummary.RestDay;

        var todayWorkout = PickWorkoutForDate(workoutDays, date);
        if (todayWorkout is null)
        {
            return new TodaysWorkoutSummary(
                "Living happy life",
                "Pure rest day",
                "Rest day",
                "0",
                "None",
                "REST");
        }

        var completedLog = workoutLogs
            .Where(log => log.WorkoutDayId == todayWorkout.Id)
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();

        if (todayWorkout.WorkoutType is WorkoutType.Rest)
        {
            return new TodaysWorkoutSummary(
                "Living happy life",
                "Pure rest day",
                "Rest day",
                "0",
                "None",
                BuildSessionProgressText(completedLog));
        }

        var exerciseCount = todayWorkout.WorkoutDayExercises.Count;
        return new TodaysWorkoutSummary(
            todayWorkout.Name,
            BuildWorkoutSubtitle(plan.Name, todayWorkout),
            BuildDurationText(todayWorkout),
            exerciseCount.ToString(),
            BuildWorkoutIntensity(phase, exerciseCount),
            BuildSessionProgressText(completedLog));
    }

    private static WorkoutDay? PickWorkoutForDate(
        IReadOnlyCollection<WorkoutDay> workoutDays,
        DateTime date)
    {
        if (workoutDays.Count == 0)
            return null;

        var dayOfWeek = (int)date.DayOfWeek;
        return workoutDays.FirstOrDefault(day => day.DayOfWeek == dayOfWeek);
    }

    private static string BuildWorkoutSubtitle(string planName, WorkoutDay workoutDay)
    {
        var exerciseNames = workoutDay.WorkoutDayExercises
            .Select(exercise => exercise.Exercise?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(2)
            .ToList();

        return exerciseNames.Count == 0
            ? planName
            : $"{planName} with {string.Join(" and ", exerciseNames)}";
    }

    private static string BuildDurationText(WorkoutDay day)
    {
        var seconds = day.WorkoutDayExercises.Sum(exercise => exercise.DurationSeconds ?? 0);
        if (seconds > 0)
            return $"{Math.Max(1, (int)Math.Ceiling(seconds / 60d))} min";

        var exerciseCount = day.WorkoutDayExercises.Count;
        return exerciseCount == 0
            ? "Flexible"
            : $"{Math.Max(20, exerciseCount * FallbackMinutesPerExercise)} min";
    }

    private static string BuildExerciseCountText(WorkoutDay day)
    {
        var count = day.WorkoutDayExercises.Count;
        return count == 1 ? "1 exercise" : $"{count} exercises";
    }

    private static string BuildDetailsText(WorkoutDay day)
    {
        var exerciseNames = day.WorkoutDayExercises
            .Select(exercise => exercise.Exercise?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(2)
            .ToList();

        if (exerciseNames.Count > 0)
            return string.Join(" and ", exerciseNames);

        return day.WorkoutDayExercises.Count > 0
            ? "Exercise details ready"
            : "Flexible recovery day";
    }

    private static string BuildWorkoutIntensity(CyclePhase phase, int exerciseCount)
    {
        if (phase is CyclePhase.Menstrual)
            return "Low";

        if (phase is CyclePhase.Ovulatory && exerciseCount >= 4)
            return "High";

        return exerciseCount >= 5 ? "High" : exerciseCount >= 3 ? "Medium" : "Low";
    }

    private static string BuildSessionProgressText(WorkoutLog? completedLog) =>
        completedLog switch
        {
            { CompletionPercent: >= 100 } => "COMPLETED",
            { CompletionPercent: > 0 } => $"{completedLog.CompletionPercent}% DONE",
            _ => "UPCOMING"
        };

    private static WorkoutListItem BuildRestDayItem(int dayOfWeek, int workoutDayId = 0)
    {
        const string activityTag = "REST";
        return new WorkoutListItem(
            workoutDayId,
            activityTag,
            FormatDayLabel(dayOfWeek),
            "Living happy life",
            "Rest day",
            "No exercises",
            "Rest, recover, enjoy the day.",
            GetActivityBackground(activityTag),
            GetActivityTextColor(activityTag),
            true);
    }

    private static string BuildActivityTag(WorkoutDay day)
    {
        if (HasActivityFocus(day, "Rock Climbing", "Climb"))
            return "CLIMB";

        if (HasActivityFocus(day, "Pilates"))
            return "PILATES";

        if (HasYogaFocus(day))
            return "YOGA";

        return day.WorkoutType switch
        {
            WorkoutType.Cardio => "CARDIO",
            WorkoutType.Recovery => "RECOVERY",
            WorkoutType.Rest => "REST",
            _ => "STRENGTH"
        };
    }

    private static bool HasYogaFocus(WorkoutDay day) =>
        HasActivityFocus(day, "Yoga");

    private static bool HasActivityFocus(WorkoutDay day, params string[] terms) =>
        terms.Any(term => day.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
        day.WorkoutDayExercises.Any(exercise =>
            terms.Any(term =>
                exercise.Exercise?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) == true));

    private static string FormatDayLabel(int dayOfWeek) =>
        dayOfWeek switch
        {
            0 => "SUN",
            1 => "MON",
            2 => "TUE",
            3 => "WED",
            4 => "THU",
            5 => "FRI",
            6 => "SAT",
            _ => $"DAY {dayOfWeek}"
        };

    private static Color GetActivityBackground(string activityTag) => activityTag switch
    {
        "CARDIO" => Color.FromArgb("#E0F2F1"),
        "CLIMB" => Color.FromArgb("#E7EEF8"),
        "PILATES" => Color.FromArgb("#F4E6D7"),
        "YOGA" => Color.FromArgb("#E8D8F5"),
        "RECOVERY" => Color.FromArgb("#E8F5E9"),
        "REST" => Color.FromArgb("#F0EFEA"),
        "STRENGTH" => Color.FromArgb("#F8DFF1"),
        _ => Color.FromArgb("#F8EEF4")
    };

    private static Color GetActivityTextColor(string activityTag) => activityTag switch
    {
        "CARDIO" => Color.FromArgb("#1F6F68"),
        "CLIMB" => Color.FromArgb("#315D87"),
        "PILATES" => Color.FromArgb("#8A5733"),
        "YOGA" => Color.FromArgb("#5A3B80"),
        "RECOVERY" => Color.FromArgb("#3A6B3A"),
        "REST" => Color.FromArgb("#5F5A50"),
        "STRENGTH" => Color.FromArgb("#8D3A5F"),
        _ => Color.FromArgb("#5B4650")
    };
}
