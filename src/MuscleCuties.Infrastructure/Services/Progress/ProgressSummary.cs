using MuscleCuties.Core.Domain.Progress;
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
        var todayDate = DateOnly.FromDateTime(today);
        var start = ProgressStreakRule.GetLookbackStart(todayDate, lookbackDays)
            .ToDateTime(TimeOnly.MinValue);
        var workoutLogs = await _workoutRepository.GetWorkoutLogsByDateRangeAsync(userId, start, today);
        var mealLogs = await _nutritionRepository.GetLoggedMealsByDateRangeAsync(userId, start, today);
        var completedWorkoutLogs = workoutLogs
            .Where(log => log.CompletionPercent >= 100)
            .ToList();
        var completedSessionCount = completedWorkoutLogs
            .Select(log => new { log.Date.Date, log.WorkoutDayId })
            .Distinct()
            .Count();

        return new ProgressSummary(
            completedSessionCount,
            ProgressStreakRule.CountCurrentStreak(
                completedWorkoutLogs.Select(log => DateOnly.FromDateTime(log.Date)), todayDate),
            ProgressStreakRule.CountCurrentStreak(
                mealLogs.Select(meal => DateOnly.FromDateTime(meal.LoggedAt)), todayDate));
    }
}
