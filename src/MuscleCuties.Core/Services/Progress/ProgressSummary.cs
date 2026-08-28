using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Services.Progress;

public sealed record ProgressSummary(
    int CompletedWorkoutSessions,
    int WorkoutStreakDays,
    int NutritionStreakDays);

public interface IProgressSummaryService
{
    Task<ProgressSummary> GetSummaryAsync(int userId, DateTime date, int lookbackDays = 90);
}

public sealed class ProgressSummaryService : IProgressSummaryService
{
    private readonly INutritionRepository _nutritionRepository;
    private readonly IWorkoutRepository _workoutRepository;

    public ProgressSummaryService(
        INutritionRepository nutritionRepository,
        IWorkoutRepository workoutRepository)
    {
        _nutritionRepository = nutritionRepository;
        _workoutRepository = workoutRepository;
    }

    public async Task<ProgressSummary> GetSummaryAsync(
        int userId,
        DateTime date,
        int lookbackDays = 90)
    {
        var today = date.Date;
        var start = today.AddDays(-Math.Max(1, lookbackDays) + 1);
        var workoutLogs = await _workoutRepository.GetWorkoutLogsByDateRangeAsync(userId, start, today);
        var mealLogs = await _nutritionRepository.GetLoggedMealsByDateRangeAsync(userId, start, today);
        var completedWorkoutLogs = workoutLogs
            .Where(log => log.CompletionPercent >= 100)
            .ToList();
        var completedSessionCount = completedWorkoutLogs
            .Select(log => new { Date = log.Date.Date, log.WorkoutDayId })
            .Distinct()
            .Count();

        return new ProgressSummary(
            completedSessionCount,
            CountCurrentStreak(completedWorkoutLogs.Select(log => log.Date), today),
            CountCurrentStreak(mealLogs.Select(meal => meal.LoggedAt), today));
    }

    private static int CountCurrentStreak(IEnumerable<DateTime> dates, DateTime today)
    {
        var loggedDays = dates
            .Select(date => date.Date)
            .ToHashSet();
        var anchor = loggedDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        for (var day = anchor; loggedDays.Contains(day); day = day.AddDays(-1))
            streak++;

        return streak;
    }
}
