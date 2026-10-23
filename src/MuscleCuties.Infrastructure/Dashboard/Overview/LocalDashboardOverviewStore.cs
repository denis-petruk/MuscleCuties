using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Progress;

namespace MuscleCuties.Infrastructure.Dashboard.Overview;

public sealed class LocalDashboardOverviewStore(IServiceScopeFactory scopeFactory) : IDashboardOverviewStore
{
    public async Task<DashboardOverviewResponse> LoadAsync(DashboardOverviewRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var userId = await RunScopedAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync());
        if (userId <= 0)
            throw new UnauthorizedAccessException("Sign in to load the dashboard.");

        var today = request.Today;
        var profileTask =
            RunScopedAsync(sp => sp.GetRequiredService<IProfileSettingsService>().GetProfileAsync(userId));
        var checkInTask = RunScopedAsync(sp =>
            sp.GetRequiredService<IDailyCheckInDataService>().GetCheckInAsync(userId, today));
        var injuryTask = RunScopedAsync(sp => sp.GetRequiredService<IWorkoutInjuryService>().GetActiveAsync(userId));
        var predictionTask = RunScopedAsync(sp => sp.GetRequiredService<ICycleService>().GetPredictionAsync(userId));
        var consumedTask = RunScopedAsync(sp => sp.GetRequiredService<INutritionService>()
            .GetConsumedTotalsAsync(userId, today.ToDateTime(TimeOnly.MinValue)));
        var progressTask = RunScopedAsync(sp => sp.GetRequiredService<IProgressSummaryUseCase>()
            .GetSummaryAsync(today, 90, ct));
        var cycleHistoryTask =
            RunScopedAsync(sp => sp.GetRequiredService<ICycleService>().GetCycleHistoryAsync(userId));
        var trendTask = RunScopedAsync(sp => sp.GetRequiredService<IDailyCheckInDataService>()
            .GetCheckInsAsync(userId, today.AddDays(-12), today));
        var photoTask = RunScopedAsync(sp => sp.GetRequiredService<IPhotoVaultUseCase>().LoadAsync(ct));

        await Task.WhenAll(profileTask, checkInTask, injuryTask, predictionTask, consumedTask,
            progressTask, cycleHistoryTask, trendTask, photoTask).ConfigureAwait(false);

        var profile = await profileTask.ConfigureAwait(false);
        var checkIn = await checkInTask.ConfigureAwait(false);
        var prediction = await predictionTask.ConfigureAwait(false) ??
                         CreateDefaultPrediction(profile?.CycleLength ?? 0);
        var phase = prediction.CurrentPhase;

        var targetsTask = RunScopedAsync(sp => sp.GetRequiredService<INutritionService>()
            .CalculateDailyTargetsAsync(userId, phase));
        var workoutTask = RunScopedAsync(sp => sp.GetRequiredService<IWorkoutService>()
            .GetTodaysSummaryAsync(userId, phase, today.ToDateTime(TimeOnly.MinValue)));
        await Task.WhenAll(targetsTask, workoutTask).ConfigureAwait(false);

        var targets = await targetsTask.ConfigureAwait(false);
        var consumed = await consumedTask.ConfigureAwait(false);
        var workout = await workoutTask.ConfigureAwait(false);
        var trends = await trendTask.ConfigureAwait(false);
        var (averageSleep, steps) = ComputeSleepAndSteps(checkIn, trends);
        var calorieProgress = ComputeCalorieProgress(consumed.Calories, targets.Calories);

        var activeInjuries = await injuryTask.ConfigureAwait(false);
        var hasActiveInjury = activeInjuries.Count > 0;
        var support = await ResolveSupportSummaryAsync(userId, prediction, phase, calorieProgress,
            profile, workout, checkIn?.ReadinessScore, hasActiveInjury, steps, averageSleep, ct).ConfigureAwait(false);

        if (await RunScopedAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync())
                .ConfigureAwait(false) != userId)
            throw new UnauthorizedAccessException("Account changed while loading the dashboard.");

        var trendPoints = BuildTrendPoints(trends, today);
        var photos = await photoTask.ConfigureAwait(false);
        var cycleHistory = await cycleHistoryTask.ConfigureAwait(false);
        var progress = await progressTask.ConfigureAwait(false);

        return BuildOverviewResponse(profile, prediction, phase, checkIn, hasActiveInjury,
            consumed, targets, progress, photos, support, workout, trendPoints, cycleHistory.Count, today);
    }

    public async Task<DashboardCalendarResponse> LoadCalendarAsync(DashboardCalendarRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var userId = await RunScopedAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync());
        if (userId <= 0)
            throw new UnauthorizedAccessException("Sign in to load the calendar.");
        var month = new DateTime(request.Year, request.Month, 1);
        var offset = ((int)month.DayOfWeek + 6) % 7;
        var cellCount = (int)Math.Ceiling((offset + DateTime.DaysInMonth(month.Year, month.Month)) / 7d) * 7;
        var start = month.AddDays(-offset);
        var end = start.AddDays(cellCount - 1);
        var workoutsTask = RunScopedAsync(sp => sp.GetRequiredService<IDashboardCalendarDataService>()
            .GetWorkoutDatesAsync(userId, start, end));
        var mealsTask = RunScopedAsync(sp => sp.GetRequiredService<IDashboardCalendarDataService>()
            .GetMealDatesAsync(userId, start, end));
        var plannedTask = RunScopedAsync(sp => sp.GetRequiredService<IDashboardCalendarDataService>()
            .GetPlannedWorkoutWeekdaysAsync(userId));
        await Task.WhenAll(workoutsTask, mealsTask, plannedTask).ConfigureAwait(false);
        var workouts = await workoutsTask.ConfigureAwait(false);
        var meals = await mealsTask.ConfigureAwait(false);
        var planned = await plannedTask.ConfigureAwait(false);
        if (await RunScopedAsync(sp => sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync())
                .ConfigureAwait(false) != userId)
            throw new UnauthorizedAccessException("Account changed while loading the calendar.");
        var days = Enumerable.Range(0, cellCount).Select(index =>
        {
            var date = start.AddDays(index).Date;
            var dateOnly = DateOnly.FromDateTime(date);
            var hasWorkout = workouts.Contains(date);
            var isCurrentMonth = date.Month == month.Month && date.Year == month.Year;
            return new DashboardCalendarDayDto(dateOnly, isCurrentMonth,
                dateOnly == request.Today, hasWorkout,
                isCurrentMonth && planned.Contains((int)date.DayOfWeek) && !hasWorkout && dateOnly >= request.Today,
                meals.Contains(date));
        }).ToArray();
        return new DashboardCalendarResponse(days);
    }

    private Task<T> RunScopedAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        return ScopedAsync(scopeFactory, action);
    }

    private static async Task<T> ScopedAsync<T>(IServiceScopeFactory factory, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.CreateAsyncScope();
        return await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    private static (double? AverageSleep, int? Steps) ComputeSleepAndSteps(
        DailyReadinessLog? checkIn,
        IReadOnlyList<DailyReadinessLog> trends)
    {
        var finiteSleep = trends.Where(item => item.SleepHours > 0 && double.IsFinite(item.SleepHours)).ToArray();
        double? averageSleep = finiteSleep.Length == 0 ? null : finiteSleep.Average(item => item.SleepHours);
        var steps = checkIn?.StepsYesterday > 0 ? checkIn.StepsYesterday :
            checkIn?.Steps7dAvg > 0 ? checkIn.Steps7dAvg :
            trends.Where(item => item.StepsYesterday > 0).Select(item => (int?)item.StepsYesterday).LastOrDefault();
        return (averageSleep, steps);
    }

    private static float ComputeCalorieProgress(float consumedCalories, float targetCalories)
    {
        return targetCalories <= 0f || !float.IsFinite(targetCalories)
            ? 0f
            : Math.Clamp(consumedCalories / targetCalories, 0f, 1f);
    }

    private static CyclePrediction CreateDefaultPrediction(int cycleLength)
    {
        return new CyclePrediction
        {
            CurrentPhase = CyclePhase.Follicular,
            PredictedCycleLength = cycleLength > 0 ? cycleLength : 28,
            PredictionSource = "default"
        };
    }

    private static DashboardTrendPointDto[] BuildTrendPoints(
        IReadOnlyList<DailyReadinessLog> trends,
        DateOnly today)
    {
        return DashboardTrendPlanner
            .Build(trends, today)
            .Select(point => new DashboardTrendPointDto(point.Date, point.WeightKg, point.ReadinessScore))
            .ToArray();
    }

    private Task<DashboardSupportSummary> ResolveSupportSummaryAsync(
        int userId,
        CyclePrediction prediction,
        CyclePhase phase,
        float calorieProgress,
        UserProfile? profile,
        TodaysWorkoutSummary workout,
        int? readinessScore,
        bool hasActiveInjury,
        int? steps,
        double? averageSleep,
        CancellationToken ct)
    {
        return RunScopedAsync(async sp =>
        {
            var sync = sp.GetService<IReadinessSyncOrchestrator>();
            if (sync is not null)
                return await sync.ComputeDashboardSupportAsync(userId, prediction, phase, calorieProgress,
                    profile?.Weight, profile?.WorkoutDaysPerWeek ?? -1, workout, readinessScore,
                    hasActiveInjury, steps, averageSleep, ct).ConfigureAwait(false);

            return sp.GetRequiredService<IDashboardPlanner>().BuildSupportSummary(prediction, phase,
                calorieProgress, profile?.Weight, profile?.WorkoutDaysPerWeek ?? -1, workout,
                recordedReadinessScore: readinessScore,
                hasActiveInjury: hasActiveInjury,
                localSteps: steps, localAverageSleepHours: averageSleep);
        });
    }

    private static DashboardOverviewResponse BuildOverviewResponse(
        UserProfile? profile,
        CyclePrediction prediction,
        CyclePhase phase,
        DailyReadinessLog? checkIn,
        bool hasActiveInjury,
        MacroNutrients consumed,
        (float Calories, float Protein, float Carbs, float Fats) targets,
        ProgressSummaryDto progress,
        PhotoVaultLoadResult photos,
        DashboardSupportSummary support,
        TodaysWorkoutSummary workout,
        DashboardTrendPointDto[] trendPoints,
        int cycleHistoryCount,
        DateOnly today)
    {
        var sections = (workout.ActivitySections ?? [])
            .Select(section => new DashboardWorkoutActivityDto(section.Tag, section.Title, section.ExerciseCount))
            .ToArray();

        return new DashboardOverviewResponse(
            FirstName(profile?.Name), phase.ToString(), prediction.PredictionSource,
            prediction.HasActiveCycle,
            prediction.CurrentCycleStartDate is { } cycleStart ? DateOnly.FromDateTime(cycleStart) : null,
            prediction.CurrentDay, prediction.PredictedCycleLength, prediction.DaysUntilPeriod,
            cycleHistoryCount,
            checkIn is null, hasActiveInjury,
            consumed.Calories, consumed.Carbs, consumed.Fats, consumed.Protein,
            targets.Calories, targets.Carbs, targets.Fats, targets.Protein,
            progress.WorkoutStreakDays,
            progress.NutritionStreakDays,
            photos.Photos.Any(photo => photo.Date.Date == today.ToDateTime(TimeOnly.MinValue).Date),
            support.CycleInsightText, support.HydrationConsumed, support.HydrationGoal, support.SleepGoal,
            support.ReadinessScore, support.ReadinessLabel, support.RecoveryScore, support.RecoveryLabel,
            workout.Title, workout.Subtitle, workout.DurationText, workout.ExercisesCount,
            workout.Intensity, workout.SessionProgressText, workout.ActivityTag, workout.WorkoutDayId,
            sections, trendPoints);
    }

    private static string FirstName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? string.Empty
            : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }
}
