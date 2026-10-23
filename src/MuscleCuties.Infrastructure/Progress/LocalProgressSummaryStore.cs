using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Progress;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.Infrastructure.Progress;

public sealed class LocalProgressSummaryStore : IProgressSummaryStore
{
    private const string WorkoutHistoryCountRoute = "/api/v1/workouts/history/count";
    private readonly IAuthService _authService;
    private readonly AppDatabase? _db;
    private readonly IMuscleCutiesBackendApiClient? _apiClient;
    private readonly MobileJourneyCloudGateway? _cloudGateway;

    public LocalProgressSummaryStore(
        IAuthService authService,
        AppDatabase? db = null,
        IMuscleCutiesBackendApiClient? apiClient = null,
        MobileJourneyCloudGateway? cloudGateway = null)
    {
        _authService = authService;
        _db = db;
        _apiClient = apiClient;
        _cloudGateway = cloudGateway;
    }

    public LocalProgressSummaryStore(AppDatabase db, IAuthService authService)
        : this(authService, db, null, null)
    {
    }

    public async Task<ProgressSummaryDto> GetSummaryAsync(
        DateOnly date,
        int lookbackDays = 90,
        CancellationToken ct = default)
    {
        if (_db is null && _apiClient is not null && _cloudGateway is not null)
        {
            var dashboardTask = _cloudGateway.GetDashboardOverviewAsync(date, ct);
            var countTask = _apiClient.GetAsync<WorkoutHistoryCountDto>(WorkoutHistoryCountRoute, ct);
            await Task.WhenAll(dashboardTask, countTask).ConfigureAwait(false);
            var dashboard = CloudStoreResponse.RequireData(await dashboardTask.ConfigureAwait(false), "progress dashboard");
            var count = CloudStoreResponse.RequireData(await countTask.ConfigureAwait(false), "progress workout count");
            return new ProgressSummaryDto(
                count.Count,
                dashboard.WorkoutStreakDays,
                dashboard.NutritionStreakDays);
        }

        if (_db is null)
            throw new InvalidOperationException("Progress summary store requires a configured backend API client or database.");

        var userId = await RequireCurrentUserIdAsync().ConfigureAwait(false);
        var today = date.ToDateTime(TimeOnly.MinValue);
        var start = ProgressStreakRule.GetLookbackStart(date, lookbackDays)
            .ToDateTime(TimeOnly.MinValue);

        var rangeStart = start.Date;
        var rangeEnd = today.Date.AddTicks(TimeSpan.TicksPerDay - 1);

        var workoutLogs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.Date >= rangeStart && w.Date <= rangeEnd)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var mealLogs = await _db.LoggedMeals
            .AsNoTracking()
            .Where(m => m.UserId == userId &&
                        ((m.LoggedAt != default && m.LoggedAt >= rangeStart && m.LoggedAt <= rangeEnd) ||
                         (m.LoggedAt == default && m.Date >= rangeStart && m.Date <= rangeEnd)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnsureCurrentUserUnchangedAsync(userId).ConfigureAwait(false);

        var completedWorkoutLogs = workoutLogs
            .Where(log => log.CompletionPercent >= 100)
            .ToList();

        var completedSessionCount = completedWorkoutLogs
            .Select(log => new { log.Date.Date, log.WorkoutDayId })
            .Distinct()
            .Count();

        var workoutStreak = ProgressStreakRule.CountCurrentStreak(
            completedWorkoutLogs.Select(log => DateOnly.FromDateTime(log.Date)), date);

        var nutritionStreak = ProgressStreakRule.CountCurrentStreak(
            mealLogs.Select(meal => DateOnly.FromDateTime(meal.LoggedAt)), date);

        return new ProgressSummaryDto(
            completedSessionCount,
            workoutStreak,
            nutritionStreak);
    }

    private async Task<int> RequireCurrentUserIdAsync()
    {
        var id = await _authService.GetCurrentUserIdAsync().ConfigureAwait(false);
        if (id <= 0)
            throw new UnauthorizedAccessException("Sign in to access progress summary.");
        return id;
    }

    private async Task EnsureCurrentUserUnchangedAsync(int expectedUserId)
    {
        if (await RequireCurrentUserIdAsync().ConfigureAwait(false) != expectedUserId)
            throw new UnauthorizedAccessException("Account changed during progress summary operation.");
    }
}
