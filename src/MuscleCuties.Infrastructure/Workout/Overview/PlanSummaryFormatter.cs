using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Workout.Overview;

/// <summary>
///     Production formatter for the Workouts tab plan summary. It reads the overview response so
///     the count the user sees is the count the overview actually carries.
/// </summary>
public static class PlanSummaryFormatter
{
    public static string Format(WorkoutOverviewResponse overview, int weekIndex)
    {
        var weekSchedule = overview.Schedule.Where(day => day.WeekIndex == weekIndex).ToArray();
        var scheduleIds = weekSchedule.Where(day => !day.IsRestDay)
            .Select(day => day.WorkoutDayId).ToHashSet();
        var weekWorkouts = weekSchedule.Length > 0
            ? overview.Workouts.Where(workout => scheduleIds.Contains(workout.WorkoutDayId)).ToArray()
            : overview.Workouts.ToArray();

        var trainingDayCount = weekSchedule.Length > 0
            ? weekSchedule.Count(day => !day.IsRestDay)
            : weekWorkouts.Where(workout => !workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        if (trainingDayCount == 0)
            return "No sessions scheduled yet";

        var fullRestDayCount = weekSchedule.Length > 0
            ? weekSchedule.Count(day => day.IsRestDay)
            : weekWorkouts.Where(workout => workout.IsRestDay)
                .Select(workout => workout.DayLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        var activityCount = weekWorkouts.Where(workout => !workout.IsRestDay)
            .Sum(workout => workout.ActivityCount);

        var parts = new List<string>
        {
            $"{trainingDayCount} training {(trainingDayCount == 1 ? "day" : "days")}",
            $"{fullRestDayCount} full rest {(fullRestDayCount == 1 ? "day" : "days")}"
        };
        if (activityCount > trainingDayCount)
            parts.Add($"{activityCount} activities");

        return string.Join(" - ", parts);
    }
}
