using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    public IReadOnlyList<WorkoutListItem> BuildWorkoutItems(
        IReadOnlyCollection<WorkoutDay> workoutDays,
        IReadOnlyCollection<WorkoutLog>? workoutLogs = null)
    {
        var daysByWeekday = workoutDays
            .GroupBy(day => day.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.First());
        var latestLogByWorkoutDayId = (workoutLogs ?? [])
            .Where(log => log.WorkoutDayId > 0)
            .GroupBy(log => log.WorkoutDayId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(log => log.Date)
                    .ThenByDescending(log => log.CreatedAt)
                    .First());

        return Enumerable.Range(0, 7)
            .Select(dayOfWeek =>
            {
                if (!daysByWeekday.TryGetValue(dayOfWeek, out var day))
                    return BuildRestDayItem(dayOfWeek);

                var latestLog = latestLogByWorkoutDayId.GetValueOrDefault(day.Id);
                if (day.WorkoutType is WorkoutType.Rest)
                    return BuildRestDayItem(day.DayOfWeek, day.Id, latestLog);

                var activityTag = WorkoutActivityClassifier.BuildPrimaryTag(day);
                return new WorkoutListItem(
                    day.Id,
                    activityTag,
                    FormatDayLabel(day.DayOfWeek),
                    day.Name,
                    BuildDurationText(day),
                    BuildExerciseCountText(day),
                    BuildActivityCountText(day),
                    BuildDetailsText(day),
                    WorkoutActivityClassifier.GetBackground(activityTag),
                    WorkoutActivityClassifier.GetTextColor(activityTag),
                    false,
                    BuildSessionProgressText(latestLog),
                    IsWorkoutCompleted(latestLog));
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
                "REST",
                WorkoutActivityClassifier.RestTag);
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
                BuildSessionProgressText(completedLog),
                WorkoutActivityClassifier.RestTag);
        }

        var exerciseCount = todayWorkout.WorkoutDayExercises.Count;
        var activityTag = WorkoutActivityClassifier.BuildPrimaryTag(todayWorkout);
        return new TodaysWorkoutSummary(
            todayWorkout.Name,
            BuildWorkoutSubtitle(todayWorkout),
            BuildDurationText(todayWorkout),
            exerciseCount.ToString(),
            BuildWorkoutIntensity(phase, exerciseCount),
            BuildSessionProgressText(completedLog),
            activityTag);
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

    private static string BuildWorkoutSubtitle(WorkoutDay workoutDay)
    {
        var exerciseNames = workoutDay.WorkoutDayExercises
            .Select(exercise => exercise.Exercise?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Take(2)
            .ToList();

        if (workoutDay.WorkoutType is WorkoutType.Recovery)
            return "Easy movement to improve circulation and leave you fresher.";

        if (workoutDay.WorkoutType is WorkoutType.Cardio)
            return BuildCardioSubtitle(workoutDay);

        return BuildStrengthSubtitle(workoutDay, exerciseNames);
    }

    private static string BuildCardioSubtitle(WorkoutDay workoutDay)
    {
        if (HasActivityFocus(workoutDay, "HIIT", "Interval", "Sprint"))
            return "Short conditioning blocks with recovery kept honest.";

        if (HasActivityFocus(workoutDay, "Ride", "Zone 2", "Cycling"))
            return "Steady aerobic work at a repeatable, conversational pace.";

        if (HasActivityFocus(workoutDay, "Run", "Jog"))
            return "Pace work with enough control to recover and repeat.";

        if (HasActivityFocus(workoutDay, "Swimming"))
            return "Pool conditioning with relaxed shoulders and clean breathing.";

        return "Conditioning work paced so you can actually recover from it.";
    }

    private static string BuildStrengthSubtitle(
        WorkoutDay workoutDay,
        IReadOnlyList<string> exerciseNames)
    {
        if (HasActivityFocus(workoutDay, "Leg", "Squat", "Lunge", "Glute", "Hip Thrust"))
            return "Legs and glutes first, with core work to keep the lift honest.";

        if (HasActivityFocus(workoutDay, "Upper", "Row", "Pulldown", "Press", "Face Pull"))
            return "Push and pull strength with enough shoulder balance.";

        if (exerciseNames.Count > 0)
            return $"{string.Join(" and ", exerciseNames)} anchor the session.";

        return "Strength work with clear sets, reps, and room to progress.";
    }

    private static string BuildDurationText(WorkoutDay day)
    {
        var seconds = day.WorkoutDayExercises.Sum(exercise => exercise.DurationSeconds ?? 0);
        var hasStrengthWork = day.WorkoutDayExercises.Any(exercise => exercise.Sets > 0 && exercise.Reps > 0);
        if (seconds > 0 && hasStrengthWork)
        {
            var strengthMinutes = day.WorkoutType is WorkoutType.Strength
                ? WorkoutDurationEstimator.EstimateStrengthMinutes(day.WorkoutDayExercises)
                : 0;
            return $"{Math.Max(1, strengthMinutes + (int)Math.Ceiling(seconds / 60d))} min";
        }

        if (seconds > 0)
            return $"{Math.Max(1, (int)Math.Ceiling(seconds / 60d))} min";

        if (day.WorkoutType is WorkoutType.Strength)
            return $"{WorkoutDurationEstimator.EstimateStrengthMinutes(day.WorkoutDayExercises)} min";

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

    private static string BuildActivityCountText(WorkoutDay day)
    {
        if (day.WorkoutType is WorkoutType.Rest)
            return "0 activities";

        var count = CountActivityGroups(day);
        return count == 1 ? "1 activity" : $"{count} activities";
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
            { CompletionPercent: >= 100 } => "Completed",
            { CompletionPercent: > 0 } => $"{completedLog.CompletionPercent}% done",
            _ => "Upcoming"
        };

    private static bool IsWorkoutCompleted(WorkoutLog? completedLog) =>
        completedLog?.CompletionPercent >= 100;

    private static WorkoutListItem BuildRestDayItem(
        int dayOfWeek,
        int workoutDayId = 0,
        WorkoutLog? latestLog = null)
    {
        const string activityTag = WorkoutActivityClassifier.RestTag;
        return new WorkoutListItem(
            workoutDayId,
            activityTag,
            FormatDayLabel(dayOfWeek),
            "Living happy life",
            "Rest day",
            "No exercises",
            "0 activities",
            "Rest, recover, enjoy the day.",
            WorkoutActivityClassifier.GetBackground(activityTag),
            WorkoutActivityClassifier.GetTextColor(activityTag),
            true,
            BuildSessionProgressText(latestLog),
            IsWorkoutCompleted(latestLog));
    }

    private static bool HasActivityFocus(WorkoutDay day, params string[] terms) =>
        terms.Any(term => day.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
        day.WorkoutDayExercises.Any(exercise =>
            terms.Any(term =>
                exercise.Exercise?.Name.Contains(term, StringComparison.OrdinalIgnoreCase) == true));

    private static int CountActivityGroups(WorkoutDay day) =>
        WorkoutActivityClassifier.BuildActivityTags(day).Count(tag => tag != WorkoutActivityClassifier.RestTag);

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

}
