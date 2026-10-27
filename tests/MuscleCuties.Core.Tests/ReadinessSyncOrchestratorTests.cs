using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Inputs;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Models.UI.Workout;
using MuscleCuties.Core.Models.Workout.Logging;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Infrastructure.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Core.Tests;

public sealed class ReadinessSyncOrchestratorTests
{
    [Fact]
    public async Task SyncIfStale_Triggers_Sync_When_Data_Is_Older_Than_Three_Hours()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7), DateTime.Today, 7000, 7.5, 72, 65, 0,
                DateTime.UtcNow, 6500, 7.2, 280)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        var orchestrator = CreateOrchestrator(healthSync);

        // First direct sync
        await healthSync.SyncAsync(1, HealthDataSource.AppleHealth);

        // Immediate check (fresh) -> should not trigger sync again
        provider.ResetReadCalled();
        var summaryFresh = await orchestrator.SyncIfStaleAndGetSummaryAsync(1, TimeSpan.FromHours(3));
        Assert.NotNull(summaryFresh);
        Assert.False(provider.ReadCalled);

        // Fast-forward staleness: max age 0 seconds
        var summaryStale = await orchestrator.SyncIfStaleAndGetSummaryAsync(1, TimeSpan.Zero);
        Assert.NotNull(summaryStale);
        Assert.True(provider.ReadCalled);
    }

    [Fact]
    public async Task GetProfileHealthSyncSummary_Aggregates_All_Domain_Services()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7), DateTime.Today, 8000, 8.0, 68, 60, 0,
                DateTime.UtcNow, 7500, 7.8, 300)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        await healthSync.SyncAsync(42, HealthDataSource.AppleHealth);

        var orchestrator = CreateOrchestrator(healthSync);
        var summary = await orchestrator.GetProfileHealthSyncSummaryAsync(42);

        Assert.True(summary.Status.IsConnected);
        Assert.NotNull(summary.HealthSummary);
        Assert.Contains("Apple Health", summary.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Connected", summary.ConnectionBadgeText);
        Assert.Contains("Last sync", summary.LastSyncedText, StringComparison.OrdinalIgnoreCase);
        Assert.True(summary.ReadinessScore > 0);
        Assert.False(string.IsNullOrWhiteSpace(summary.ReadinessExplanationText));
    }

    [Fact]
    public async Task ProfileHealthSyncUseCase_Delegates_To_Orchestrator_And_Maps_Summary()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7), DateTime.Today, 8000, 8.0, 68, 60, 0,
                DateTime.UtcNow, 7500, 7.8, 300)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        await healthSync.SyncAsync(42, HealthDataSource.AppleHealth);

        var orchestrator = CreateOrchestrator(healthSync);
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentProfileUserIdProvider>(new StubCurrentProfileUserIdProvider(42));
        services.AddSingleton<IReadinessSyncOrchestrator>(orchestrator);
        using var providerScope = services.BuildServiceProvider();

        var store = new LocalProfileHealthSyncStore(providerScope.GetRequiredService<IServiceScopeFactory>());
        var useCase = new ProfileHealthSyncUseCase(store);

        var loaded = await useCase.LoadAsync();

        Assert.Equal("Refresh steps", loaded.ConnectionBadgeText);
        Assert.True(loaded.ReadinessScore > 0);
        Assert.False(string.IsNullOrWhiteSpace(loaded.ReadinessExplanationText));
    }

    [Fact]
    public async Task HealthReadinessBridge_With_Orchestrator_Enriches_DailyReadinessLog()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7), DateTime.Today, 9000, 7.0, 70, 62, 0,
                DateTime.UtcNow, 8500, 7.5, 310, 7.1)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        await healthSync.SyncAsync(10, HealthDataSource.AppleHealth);

        var orchestrator = CreateOrchestrator(healthSync);
        var bridge = new HealthReadinessBridge(healthSync, orchestrator);
        var log = new DailyReadinessLog();

        var enriched = await bridge.EnrichAsync(10, log);

        Assert.NotNull(enriched);
        Assert.Equal(9000, log.Steps7dAvg);
        Assert.Equal(8500, log.StepsYesterday);
        Assert.Equal(0, log.Sleep3dAvg);
        Assert.Equal(0, log.SleepHours);
    }

    [Fact]
    public async Task AppleHealthSummary_Strips_NonStep_Metrics_Before_Caching()
    {
        var provider = new TestProvider
        {
            Summary = new HealthWeeklySummary(
                HealthDataSource.AppleHealth,
                DateTime.Today.AddDays(-7),
                DateTime.Today,
                8400,
                7.6,
                76,
                58,
                0,
                DateTime.UtcNow,
                9100,
                7.8,
                420,
                7.7)
        };
        var healthSync = new HealthSyncService([provider], new MemoryTokenStorage());
        var result = await healthSync.SyncAsync(55, HealthDataSource.AppleHealth);
        var status = await healthSync.GetStatusAsync(55);

        Assert.True(result.IsConnected);
        Assert.Contains($"{provider.Summary.AverageSteps:N0} avg steps", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sleep", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RHR", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active", status.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Summary);
        Assert.False(result.Summary.HasSleepData);
        Assert.Equal(0, result.Summary.RestingHeartRate);
        Assert.Null(result.Summary.AverageActiveEnergyKcal);

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentProfileUserIdProvider>(new StubCurrentProfileUserIdProvider(55));
        services.AddSingleton<IHealthSyncService>(healthSync);
        using var providerScope = services.BuildServiceProvider();

        var store = new LocalProfileHealthSyncStore(providerScope.GetRequiredService<IServiceScopeFactory>());
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("Refresh steps", loaded.ConnectionBadgeText);
        Assert.True(loaded.ReadinessScore > 0);
        Assert.Contains("Synced steps", loaded.ReadinessExplanationText, StringComparison.OrdinalIgnoreCase);
    }

    private static ReadinessSyncOrchestrator CreateOrchestrator(IHealthSyncService healthSync)
    {
        return new ReadinessSyncOrchestrator(
            healthSync,
            new StubProfileAccount(),
            new StubCycleService(),
            new StubNutritionService(),
            new StubWorkoutService(),
            new DashboardPlanner());
    }

    private sealed class StubCurrentProfileUserIdProvider(int userId) : ICurrentProfileUserIdProvider
    {
        public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(userId);
    }

    private sealed class StubProfileAccount : IProfileAccountService
    {
        public Task<User?> GetUserAsync(int userId) => Task.FromResult<User?>(new User { Id = userId });
        public Task<UserProfile?> GetProfileAsync(int userId) =>
            Task.FromResult<UserProfile?>(new UserProfile { UserId = userId, Weight = 65f, WorkoutDaysPerWeek = 4 });
        public Task SaveUserAsync(int userId, User user) => Task.CompletedTask;
        public Task SaveProfileAsync(int userId, UserProfile profile, bool isNewProfile) => Task.CompletedTask;
        public Task AddSnapshotAsync(int userId, UserProfileSnapshot snapshot) => Task.CompletedTask;
        public Task<string?> UpdateProfileImageAsync(int userId, string? imagePath) => Task.FromResult<string?>(null);
    }

    private sealed class StubCycleService : ICycleService
    {
        public Task<CyclePrediction> GetPredictionAsync(int userId) =>
            Task.FromResult(new CyclePrediction { CurrentPhase = CyclePhase.Follicular, CurrentDay = 8 });
        public Task<CyclePhase> GetCurrentPhaseAsync(int userId) => Task.FromResult(CyclePhase.Follicular);
        public Task<CyclePhase> GetPhaseForDateAsync(int userId, DateTime date) => Task.FromResult(CyclePhase.Follicular);
        public Task<IReadOnlyList<CycleLog>> GetCycleHistoryAsync(int userId) => Task.FromResult<IReadOnlyList<CycleLog>>([]);
        public Task<CyclePhaseLog?> GetLatestPhaseLogAsync(int userId) => Task.FromResult<CyclePhaseLog?>(null);
        public Task<IReadOnlyList<CyclePhaseLog>> GetRecentPhaseLogsAsync(int userId, int count) => Task.FromResult<IReadOnlyList<CyclePhaseLog>>([]);
        public Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsThroughDateAsync(int userId, DateTime date) => Task.FromResult<IReadOnlyList<CyclePhaseLog>>([]);
        public Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate) => Task.FromResult<IReadOnlyList<CyclePhaseLog>>([]);
        public Task<IReadOnlyList<CyclePhaseLog>> GetPhaseLogsForCalendarRangeAsync(int userId, DateTime startDate, DateTime endDate) => Task.FromResult<IReadOnlyList<CyclePhaseLog>>([]);
        public Task<CyclePhaseLog?> GetPhaseLogForDateAsync(int userId, DateTime date) => Task.FromResult<CyclePhaseLog?>(null);
        public Task SetPhaseForDateAsync(int userId, CyclePhase phase, DateTime loggedAt, string? note) => Task.CompletedTask;
        public Task<IReadOnlyList<SymptomLog>> GetSymptomsForDateAsync(int userId, DateTime date) => Task.FromResult<IReadOnlyList<SymptomLog>>([]);
        public Task<IReadOnlyList<SymptomLog>> GetSymptomsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate) => Task.FromResult<IReadOnlyList<SymptomLog>>([]);
        public Task SaveDailyCycleLogAsync(int userId, DateTime date, bool? isPeriodStart, bool? isPeriodEnd, FlowIntensity? flowIntensity, IReadOnlyList<SymptomType> symptoms, CyclePhase? phaseOverride = null, string? note = null) => Task.CompletedTask;
    }

    private sealed class StubNutritionService : INutritionService
    {
        public Task<NutritionPlan> GetDailyPlanAsync(int userId, CyclePhase phase, DateTime date, BreakfastPreference breakfastPreference = BreakfastPreference.Savoury) => throw new NotImplementedException();
        public Task<MacroNutrients> GetConsumedTotalsAsync(int userId, DateTime date) =>
            Task.FromResult(new MacroNutrients(1800f, 130f, 200f, 50f));
        public Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase) =>
            Task.FromResult((2000f, 140f, 220f, 60f));
        public Task<List<FoodItem>> SearchFoodItemsAsync(string query, int pageSize = 15, int pageNumber = 1) => throw new NotImplementedException();
        public Task<FoodItem> CreateCustomFoodAsync(CustomFoodInput input) => throw new NotImplementedException();
        public Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date) => throw new NotImplementedException();
        public Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId) => throw new NotImplementedException();
        public Task<IReadOnlyList<SuggestedMeal>> GetSuggestedMealsAsync(int userId, MealType mealType, BreakfastPreference breakfastPreference, CyclePhase phase, DateTime date, IReadOnlySet<string>? excludeConceptNames = null) => throw new NotImplementedException();
        public Task<DailyMealSuggestionPlan> GetSuggestedMealPlanAsync(int userId, BreakfastPreference breakfastPreference, CyclePhase phase, DateTime date) => throw new NotImplementedException();
        public Task<IReadOnlyList<SuggestedMeal>> GetSuggestedMealsForTargetAsync(int userId, MealNutritionTarget target, BreakfastPreference breakfastPreference, CyclePhase phase, DateTime date, IReadOnlySet<string>? excludeConceptNames = null) => throw new NotImplementedException();
        public Task LogMealAsync(int userId, IReadOnlyCollection<MealIngredientInput> ingredients, MealType mealType, DateTime loggedAt, string? acceptedSuggestionName = null, CyclePhase? acceptedSuggestionPhase = null) => throw new NotImplementedException();
        public Task UpdateMealAsync(int userId, int loggedMealId, IReadOnlyCollection<MealIngredientInput> ingredients, MealType mealType, DateTime loggedAt) => throw new NotImplementedException();
        public Task DeleteMealAsync(int userId, int loggedMealId) => throw new NotImplementedException();
    }

    private sealed class StubWorkoutService : IWorkoutService
    {
        public Task<WorkoutPlanSummary> GetPlanSummaryAsync(int userId, CyclePhase phase) => throw new NotImplementedException();
        public Task<TodaysWorkoutSummary> GetTodaysSummaryAsync(int userId, CyclePhase phase, DateTime date) =>
            Task.FromResult(TodaysWorkoutSummary.RestDay);
        public Task<WorkoutSessionDetail> GetWorkoutSessionDetailAsync(int userId, int workoutDayId) => throw new NotImplementedException();
        public Task LogWorkoutSessionAsync(int userId, int workoutDayId, IReadOnlyCollection<WorkoutExerciseLogInput> exerciseLogs, DateTime date) => throw new NotImplementedException();
        public Task<IReadOnlyList<ExerciseSwapOption>> GetSwapCandidatesAsync(int userId, int workoutDayExerciseId) => throw new NotImplementedException();
        public Task SwapExerciseAsync(int userId, int workoutDayExerciseId, int newExerciseId, bool savePreference) => throw new NotImplementedException();
        public Task<IReadOnlyList<ScheduleDayItem>> GetDatedScheduleAsync(int userId, DateTime startDate, int weeksCount = 4, CyclePhase? phase = null) => throw new NotImplementedException();
        public Task RescheduleWorkoutAsync(int userId, DateTime sourceDate, DateTime targetDate) => Task.CompletedTask;
        public Task RescheduleWorkoutDayAsync(int userId, int workoutDayId, DateTime targetDate) => Task.CompletedTask;
        public Task<string?> GetMovementNotesAsync(int userId, int exerciseId) => Task.FromResult<string?>(null);
        public Task SetMovementNotesAsync(int userId, int exerciseId, string? notes) => Task.CompletedTask;
        public Task<string?> GetMovementNotesByNameAsync(int userId, string exerciseName) => Task.FromResult<string?>(null);
        public Task SetMovementNotesByNameAsync(int userId, string exerciseName, string? notes) => Task.CompletedTask;
        public Task<string?> GetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId) => Task.FromResult<string?>(null);
        public Task SetMovementNotesForDayExerciseAsync(int userId, int workoutDayExerciseId, string? notes) => Task.CompletedTask;
    }

    private sealed class TestProvider : IHealthDataProvider, IHealthAuthorizationProvider
    {
        public bool Authorized { get; set; } = true;
        public bool ReadCalled { get; private set; }
        public HealthWeeklySummary? Summary { get; set; }
        public HealthDataSource Source => HealthDataSource.AppleHealth;
        public string DisplayName => "Apple Health";
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default) => Task.FromResult(Authorized);
        public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default)
        {
            ReadCalled = true;
            return Task.FromResult(Summary);
        }
        public void ResetReadCalled() => ReadCalled = false;
    }

    private sealed class MemoryTokenStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> _values = new();
        public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
        public void Remove(string key) => _values.Remove(key);
        public void RemoveAll() => _values.Clear();
    }
}
