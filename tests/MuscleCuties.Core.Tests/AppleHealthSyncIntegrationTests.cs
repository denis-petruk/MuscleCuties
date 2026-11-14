using System.Globalization;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Core.Tests;

public sealed class AppleHealthSyncIntegrationTests
{
    private const int TestUserId = 42;
    private static readonly DateTime Today = DateTime.Today;

    #region 1. Multi-metric HealthWeeklySummary Tests

    [Fact]
    public void HealthWeeklySummary_FullMetricsConstruction_PreservesAllFields()
    {
        var syncedAt = DateTime.UtcNow;
        var weekStart = Today.AddDays(-6);
        var weekEnd = Today;

        var summary = new HealthWeeklySummary(
            Source: HealthDataSource.AppleHealth,
            WeekStart: weekStart,
            WeekEnd: weekEnd,
            AverageSteps: 10500,
            AverageSleepHours: 7.4,
            SleepQualityScore: 85,
            RestingHeartRate: 58,
            HrvScore: 62,
            SyncedAt: syncedAt,
            YesterdaySteps: 9800,
            LastNightSleepHours: 7.8,
            AverageActiveEnergyKcal: 480.0,
            AverageSleep3dHours: 7.2);

        Assert.Equal(HealthDataSource.AppleHealth, summary.Source);
        Assert.Equal(weekStart, summary.WeekStart);
        Assert.Equal(weekEnd, summary.WeekEnd);
        Assert.Equal(10500, summary.AverageSteps);
        Assert.Equal(7.4, summary.AverageSleepHours);
        Assert.Equal(85, summary.SleepQualityScore);
        Assert.Equal(58, summary.RestingHeartRate);
        Assert.Equal(62, summary.HrvScore);
        Assert.Equal(syncedAt, summary.SyncedAt);
        Assert.Equal(9800, summary.YesterdaySteps);
        Assert.Equal(7.8, summary.LastNightSleepHours);
        Assert.Equal(480.0, summary.AverageActiveEnergyKcal);
        Assert.Equal(7.2, summary.AverageSleep3dHours);
    }

    [Theory]
    [InlineData(10500, true, "10,500 avg steps")]
    [InlineData(0, false, "Steps not connected")]
    [InlineData(-100, false, "Steps not connected")]
    public void HealthWeeklySummary_MovementProperties_EvaluateCorrectly(
        int averageSteps, bool expectedHasMovement, string expectedMovementSummary)
    {
        var summary = CreateSummary(averageSteps: averageSteps);

        Assert.Equal(expectedHasMovement, summary.HasMovementData);
        Assert.Equal(expectedMovementSummary, summary.MovementSummary);
    }

    [Fact]
    public void HealthWeeklySummary_SleepProperties_AverageSleepHours_Preferred()
    {
        var summary = CreateSummary(averageSleepHours: 7.4, lastNightSleepHours: 8.0, sleepQualityScore: 80);

        Assert.True(summary.HasSleepData);
        Assert.Equal("7.4h avg sleep", summary.SleepSummary);
    }

    [Fact]
    public void HealthWeeklySummary_SleepProperties_FallbackToLastNightSleepHours()
    {
        var summary = CreateSummary(averageSleepHours: 0.0, lastNightSleepHours: 7.8, sleepQualityScore: 0);

        Assert.True(summary.HasSleepData);
        Assert.Equal("7.8h last night", summary.SleepSummary);
    }

    [Fact]
    public void HealthWeeklySummary_SleepProperties_QualityScoreOnly_HasSleepData_True_SummaryDisconnected()
    {
        var summary = CreateSummary(averageSleepHours: 0.0, lastNightSleepHours: null, sleepQualityScore: 85);

        Assert.True(summary.HasSleepData);
        Assert.Equal("Sleep not connected", summary.SleepSummary);
    }

    [Fact]
    public void HealthWeeklySummary_SleepProperties_NoData_ReturnsDisconnected()
    {
        var summary = CreateSummary(averageSleepHours: 0.0, lastNightSleepHours: null, sleepQualityScore: 0);

        Assert.False(summary.HasSleepData);
        Assert.Equal("Sleep not connected", summary.SleepSummary);
    }

    #endregion

    #region 2. HealthSyncService Multi-Metric Preservation Tests

    [Fact]
    public async Task HealthSyncService_SyncAppleHealth_PreservesAllMultiMetricFields_InCacheAndSummaryText()
    {
        var originalSummary = new HealthWeeklySummary(
            Source: HealthDataSource.AppleHealth,
            WeekStart: Today.AddDays(-6),
            WeekEnd: Today,
            AverageSteps: 8500,
            AverageSleepHours: 7.2,
            SleepQualityScore: 80,
            RestingHeartRate: 56,
            HrvScore: 65,
            SyncedAt: DateTime.UtcNow,
            YesterdaySteps: 9000,
            LastNightSleepHours: 7.5,
            AverageActiveEnergyKcal: 520.0,
            AverageSleep3dHours: 7.0);

        var provider = new TestHealthProvider(HealthDataSource.AppleHealth) { Summary = originalSummary };
        var storage = new TestTokenStorage();
        using var service = new HealthSyncService([provider], storage);

        // Act: Sync
        var result = await service.SyncAsync(TestUserId, HealthDataSource.AppleHealth);

        // Assert: Sync Result
        Assert.True(result.IsConnected);
        Assert.NotNull(result.Summary);
        Assert.Equal(8500, result.Summary.AverageSteps);
        Assert.Equal(9000, result.Summary.YesterdaySteps);
        Assert.Equal(7.5, result.Summary.LastNightSleepHours);
        Assert.Equal(7.2, result.Summary.AverageSleepHours);
        Assert.Equal(7.0, result.Summary.AverageSleep3dHours);
        Assert.Equal(80, result.Summary.SleepQualityScore);
        Assert.Equal(56, result.Summary.RestingHeartRate);
        Assert.Equal(65, result.Summary.HrvScore);
        Assert.Equal(520.0, result.Summary.AverageActiveEnergyKcal);

        // Assert: Cached summary preserved completely
        var cached = await service.GetCachedWeeklySummaryAsync(TestUserId);
        Assert.NotNull(cached);
        Assert.Equal(HealthDataSource.AppleHealth, cached.Source);
        Assert.Equal(8500, cached.AverageSteps);
        Assert.Equal(9000, cached.YesterdaySteps);
        Assert.Equal(7.5, cached.LastNightSleepHours);
        Assert.Equal(7.2, cached.AverageSleepHours);
        Assert.Equal(7.0, cached.AverageSleep3dHours);
        Assert.Equal(80, cached.SleepQualityScore);
        Assert.Equal(56, cached.RestingHeartRate);
        Assert.Equal(65, cached.HrvScore);
        Assert.Equal(520.0, cached.AverageActiveEnergyKcal);

        // Assert: GetStatusAsync reflects metrics in formatted summary text
        var status = await service.GetStatusAsync(TestUserId);
        Assert.True(status.IsConnected);
        Assert.Equal(
            "Apple Health · 8,500 avg steps · 7.2h avg sleep · 56 bpm RHR · 520 kcal active",
            status.SummaryText);
    }

    [Fact]
    public async Task HealthSyncService_StatusReflectsLastNightSleep_WhenAverageSleepHoursIsZero()
    {
        var summary = CreateSummary(
            averageSteps: 7000,
            averageSleepHours: 0.0,
            lastNightSleepHours: 7.5,
            restingHeartRate: 60,
            activeEnergyKcal: 400.0);

        var provider = new TestHealthProvider(HealthDataSource.AppleHealth) { Summary = summary };
        var storage = new TestTokenStorage();
        using var service = new HealthSyncService([provider], storage);

        await service.SyncAsync(TestUserId, HealthDataSource.AppleHealth);
        var status = await service.GetStatusAsync(TestUserId);

        Assert.True(status.IsConnected);
        Assert.Equal(
            "Apple Health · 7,000 avg steps · 7.5h last night · 60 bpm RHR · 400 kcal active",
            status.SummaryText);
    }

    #endregion

    #region 3. HealthEnergyAdjustment.CalculateDailyCalories Tests

    [Theory]
    [InlineData(8500, 3, 0f)]  // diff = +500 (< 750) -> 0
    [InlineData(7500, 3, 0f)]  // diff = -500 (< 750) -> 0
    [InlineData(8749, 3, 0f)]  // diff = +749 (< 750) -> 0
    [InlineData(7251, 3, 0f)]  // diff = -749 (< 750) -> 0
    [InlineData(7000, 1, 0f)]  // diff = 0 -> 0
    public void CalculateDailyCalories_StepDifferenceBelow750_ReturnsZero(
        int averageSteps, int workoutDays, float expectedCalories)
    {
        var summary = CreateSummary(averageSteps: averageSteps);
        var calories = HealthEnergyAdjustment.CalculateDailyCalories(
            summary, weightKg: 70f, age: 30, workoutDaysPerWeek: workoutDays, phase: CyclePhase.Follicular);

        Assert.Equal(expectedCalories, calories);
    }

    [Fact]
    public void CalculateDailyCalories_StepSurplus_ProducesPositiveAdjustment_ClampedToMax260()
    {
        // baseline = 8000 for 3 workout days. 12000 steps -> diff = 4000.
        // weight = 70kg, age = 30 (factor 1.0), phase = Follicular (factor 1.0)
        // 4000 * 70 * 0.0005 * 1.0 * 1.0 = 140 kcal.
        var summary = CreateSummary(averageSteps: 12000);
        var calories = HealthEnergyAdjustment.CalculateDailyCalories(
            summary, weightKg: 70f, age: 30, workoutDaysPerWeek: 3, phase: CyclePhase.Follicular);

        Assert.Equal(140f, calories);

        // Huge surplus: steps = 30,000, weight = 100kg -> unclamped adjustment = 1100 -> clamped to 260
        var hugeSummary = CreateSummary(averageSteps: 30000);
        var clampedCalories = HealthEnergyAdjustment.CalculateDailyCalories(
            hugeSummary, weightKg: 100f, age: 30, workoutDaysPerWeek: 3, phase: CyclePhase.Follicular);

        Assert.Equal(260f, clampedCalories);
    }

    [Fact]
    public void CalculateDailyCalories_StepDeficit_ProducesNegativeAdjustment_ClampedToMinMinus180()
    {
        // baseline = 8000 for 3 workout days. 5000 steps -> diff = -3000.
        // weight = 70kg, age = 30 (factor 1.0), phase = Follicular (factor 1.0)
        // -3000 * 70 * 0.0005 = -105 kcal -> rounded to nearest 10 = -110 kcal.
        var summary = CreateSummary(averageSteps: 5000);
        var calories = HealthEnergyAdjustment.CalculateDailyCalories(
            summary, weightKg: 70f, age: 30, workoutDaysPerWeek: 3, phase: CyclePhase.Follicular);

        Assert.Equal(-110f, calories);

        // Huge deficit: steps = 1,000, baseline = 9,000 (5 workout days) -> diff = -8000, weight = 100kg
        // unclamped adjustment = -8000 * 100 * 0.0005 = -400 -> clamped to -180
        var hugeDeficitSummary = CreateSummary(averageSteps: 1000);
        var clampedCalories = HealthEnergyAdjustment.CalculateDailyCalories(
            hugeDeficitSummary, weightKg: 100f, age: 30, workoutDaysPerWeek: 5, phase: CyclePhase.Follicular);

        Assert.Equal(-180f, clampedCalories);
    }

    [Theory]
    [InlineData(22, 10000, 3, 60f, CyclePhase.Follicular, 60f)] // age < 25 (1.05): diff 2000 * 60 * 0.0005 * 1.05 = 63 -> 60
    [InlineData(55, 10000, 3, 60f, CyclePhase.Follicular, 50f)] // age >= 50 (0.90): diff 2000 * 60 * 0.0005 * 0.90 = 54 -> 50
    [InlineData(30, 10000, 3, 60f, CyclePhase.Follicular, 60f)] // normal age (1.0): diff 2000 * 60 * 0.0005 * 1.0 = 60 -> 60
    [InlineData(30, 10000, 3, 60f, CyclePhase.Menstrual, 60f)]  // Menstrual (0.95): diff 2000 * 60 * 0.0005 * 0.95 = 57 -> 60
    [InlineData(30, 10000, 3, 60f, CyclePhase.Luteal, 60f)]     // Luteal (1.05): diff 2000 * 60 * 0.0005 * 1.05 = 63 -> 60
    public void CalculateDailyCalories_AgeFactorsAndCyclePhases(
        int age, int averageSteps, int workoutDays, float weightKg, CyclePhase phase, float expectedCalories)
    {
        var summary = CreateSummary(averageSteps: averageSteps);
        var calories = HealthEnergyAdjustment.CalculateDailyCalories(
            summary, weightKg, age, workoutDays, phase);

        Assert.Equal(expectedCalories, calories);
    }

    [Theory]
    [InlineData(true, 70f, 8000)]   // null summary
    [InlineData(false, 0f, 10000)]   // zero weight
    [InlineData(false, -50f, 10000)] // negative weight
    [InlineData(false, 70f, 0)]      // zero steps (HasMovementData false)
    [InlineData(false, 70f, -500)]   // negative steps (HasMovementData false)
    public void CalculateDailyCalories_InvalidInputs_ReturnsZero(
        bool isSummaryNull, float weightKg, int steps)
    {
        var summary = isSummaryNull ? null : CreateSummary(averageSteps: steps);
        var calories = HealthEnergyAdjustment.CalculateDailyCalories(
            summary, weightKg, age: 28, workoutDaysPerWeek: 3, phase: CyclePhase.Follicular);

        Assert.Equal(0f, calories);
    }

    #endregion

    #region 4. DailyCheckInViewModel HealthKit Behavior Tests

    [Fact]
    public async Task DailyCheckInViewModel_WithCachedLastNightSleep_PrepopulatesSleepHoursAndBadge()
    {
        var summary = CreateSummary(
            averageSteps: 8000,
            lastNightSleepHours: 7.5,
            restingHeartRate: 58,
            hrvScore: 65);

        var syncService = CreateSyncServiceWithCachedSummary(summary);
        var userIdProvider = new TestUserIdProvider(TestUserId);
        var checkInUseCase = new TestDailyCheckInUseCase();

        var vm = new DailyCheckInViewModel(
            checkInUseCase,
            invalidateDashboard: () => { },
            onCompletedAsync: () => Task.CompletedTask,
            healthSync: syncService,
            userIdProvider: userIdProvider);

        // Act
        await vm.CheckIfAlreadyCompletedAsync();

        // Assert: Sleep pre-population
        Assert.Equal(7.5, vm.SleepHours);
        Assert.True(vm.IsSleepAutoFilled);
        Assert.Equal("Auto-filled from Apple Health (7.5h)", vm.SleepAutoFilledBadgeText);
        Assert.Equal("7.5h", vm.SleepDisplay);
    }

    [Theory]
    [InlineData(8.5, 58, 65, 5, 4)] // sleep >= 8.0 & rhr <= 60 -> quality 5, sleep >= 7.5 & rhr <= 65 -> energy 4
    [InlineData(7.5, 62, 55, 4, 4)] // sleep >= 7.0 -> quality 4, sleep >= 7.5 & rhr <= 65 -> energy 4
    [InlineData(7.0, 70, 0, 4, 3)]  // sleep >= 7.0 -> quality 4, sleep >= 6.0 & rhr <= 75 -> energy 3
    [InlineData(6.2, 70, 0, 3, 3)]  // sleep >= 6.0 -> quality 3, sleep >= 6.0 & rhr <= 75 -> energy 3
    [InlineData(5.0, 78, 25, 2, 2)] // sleep >= 4.5 -> quality 2, sleep < 6.0 -> energy 2
    [InlineData(4.0, 80, 20, 1, 2)] // sleep < 4.5 -> quality 1, sleep < 6.0 -> energy 2
    public async Task DailyCheckInViewModel_Calculates_SuggestedEnergyLevel_And_SuggestedSleepQuality(
        double lastNightSleep, int rhr, int hrv, int expectedSleepQuality, int expectedEnergyLevel)
    {
        var summary = CreateSummary(
            lastNightSleepHours: lastNightSleep,
            restingHeartRate: rhr,
            hrvScore: hrv);

        var syncService = CreateSyncServiceWithCachedSummary(summary);
        var userIdProvider = new TestUserIdProvider(TestUserId);
        var checkInUseCase = new TestDailyCheckInUseCase();

        var vm = new DailyCheckInViewModel(
            checkInUseCase,
            invalidateDashboard: () => { },
            onCompletedAsync: () => Task.CompletedTask,
            healthSync: syncService,
            userIdProvider: userIdProvider);

        await vm.CheckIfAlreadyCompletedAsync();

        Assert.True(vm.HasHealthKitSuggestions);
        Assert.Equal(expectedSleepQuality, vm.SuggestedSleepQuality);
        Assert.Equal(expectedEnergyLevel, vm.SuggestedEnergyLevel);
    }

    [Fact]
    public async Task DailyCheckInViewModel_SuggestFromAppleHealthCommand_PopulatesRatings()
    {
        var summary = CreateSummary(
            lastNightSleepHours: 8.0,
            restingHeartRate: 58,
            hrvScore: 62);

        var syncService = CreateSyncServiceWithCachedSummary(summary);
        var userIdProvider = new TestUserIdProvider(TestUserId);
        var checkInUseCase = new TestDailyCheckInUseCase();

        var vm = new DailyCheckInViewModel(
            checkInUseCase,
            invalidateDashboard: () => { },
            onCompletedAsync: () => Task.CompletedTask,
            healthSync: syncService,
            userIdProvider: userIdProvider);

        await vm.CheckIfAlreadyCompletedAsync();

        // Ratings are initially null
        Assert.Null(vm.EnergyLevel);
        Assert.Null(vm.PlanningSleepQuality);

        // Act: trigger SuggestFromAppleHealthCommand
        Assert.True(vm.SuggestFromAppleHealthCommand.CanExecute(null));
        vm.SuggestFromAppleHealthCommand.Execute(null);

        // Assert: populated with suggestions
        Assert.Equal(vm.SuggestedEnergyLevel, vm.EnergyLevel);
        Assert.Equal(vm.SuggestedSleepQuality, vm.PlanningSleepQuality);
        Assert.Equal($"{vm.SuggestedEnergyLevel} / 5", vm.EnergyDisplay);
        Assert.Equal($"{vm.SuggestedSleepQuality} / 5", vm.PlanningSleepDisplay);
    }

    [Fact]
    public async Task DailyCheckInViewModel_ManualSliderAndChipChanges_OverridePrepopulatedValues()
    {
        var summary = CreateSummary(
            lastNightSleepHours: 7.5,
            restingHeartRate: 58,
            hrvScore: 65);

        var syncService = CreateSyncServiceWithCachedSummary(summary);
        var userIdProvider = new TestUserIdProvider(TestUserId);
        var checkInUseCase = new TestDailyCheckInUseCase();

        var vm = new DailyCheckInViewModel(
            checkInUseCase,
            invalidateDashboard: () => { },
            onCompletedAsync: () => Task.CompletedTask,
            healthSync: syncService,
            userIdProvider: userIdProvider);

        await vm.CheckIfAlreadyCompletedAsync();
        vm.SuggestFromAppleHealthCommand.Execute(null);

        // Pre-populated state
        Assert.Equal(7.5, vm.SleepHours);
        Assert.Equal(4, vm.EnergyLevel);
        Assert.Equal(4, vm.PlanningSleepQuality);

        // Act: user manually drags sleep slider to 6.0
        vm.SleepHours = 6.0;
        Assert.Equal(6.0, vm.SleepHours);
        Assert.Equal("6.0h", vm.SleepDisplay);

        // Act: user overrides energy and sleep quality chips
        vm.EnergyLevel = 5;
        vm.PlanningSleepQuality = 3;
        Assert.Equal(5, vm.EnergyLevel);
        Assert.Equal(3, vm.PlanningSleepQuality);
        Assert.Equal("5 / 5", vm.EnergyDisplay);
        Assert.Equal("3 / 5", vm.PlanningSleepDisplay);

        // Act: complete remaining answers and verify submission becomes enabled
        vm.PlanningSoreness = 2;
        vm.PainLevel = 0;
        vm.HasBloating = false;

        Assert.True(vm.AreAllAnswersProvided);
        Assert.True(vm.SubmitCommand.CanExecute(null));
    }

    #endregion

    #region Helper Methods and Test Stubs

    private static HealthWeeklySummary CreateSummary(
        int averageSteps = 8000,
        double averageSleepHours = 7.0,
        int sleepQualityScore = 75,
        int restingHeartRate = 60,
        int hrvScore = 50,
        int? yesterdaySteps = null,
        double? lastNightSleepHours = null,
        double? activeEnergyKcal = null,
        double? averageSleep3dHours = null)
    {
        return new HealthWeeklySummary(
            Source: HealthDataSource.AppleHealth,
            WeekStart: Today.AddDays(-6),
            WeekEnd: Today,
            AverageSteps: averageSteps,
            AverageSleepHours: averageSleepHours,
            SleepQualityScore: sleepQualityScore,
            RestingHeartRate: restingHeartRate,
            HrvScore: hrvScore,
            SyncedAt: DateTime.UtcNow,
            YesterdaySteps: yesterdaySteps,
            LastNightSleepHours: lastNightSleepHours,
            AverageActiveEnergyKcal: activeEnergyKcal,
            AverageSleep3dHours: averageSleep3dHours);
    }

    private static HealthSyncService CreateSyncServiceWithCachedSummary(HealthWeeklySummary summary)
    {
        var provider = new TestHealthProvider(HealthDataSource.AppleHealth) { Summary = summary };
        var storage = new TestTokenStorage();
        var service = new HealthSyncService([provider], storage);
        // Pre-populate cache by syncing once
        service.SyncAsync(TestUserId, HealthDataSource.AppleHealth).GetAwaiter().GetResult();
        return service;
    }

    private sealed class TestHealthProvider(HealthDataSource source) : IHealthDataProvider, IHealthAuthorizationProvider
    {
        public HealthWeeklySummary? Summary { get; set; }
        public HealthDataSource Source => source;
        public string DisplayName => source.ToDisplayName();
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default) =>
            Task.FromResult(Summary);
    }

    private sealed class TestTokenStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> _storage = new(StringComparer.Ordinal);
        public Task<string?> GetAsync(string key) => Task.FromResult(_storage.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }
        public void Remove(string key) => _storage.Remove(key);
        public void RemoveAll() => _storage.Clear();
    }

    private sealed class TestUserIdProvider(int userId) : ICurrentProfileUserIdProvider
    {
        public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(userId);
    }

    private sealed class TestDailyCheckInUseCase : IDailyCheckInUseCase
    {
        public DailyCheckInPresentationSettings PresentationSettings { get; set; } = new(true, true);
        public DailyCheckInStatusResponse StatusResponse { get; set; } = new(false, "", "", null);
        public SubmitDailyCheckInResponse SubmitResponse { get; set; } = new(80, "FeelingReady", "Great session", false, null);

        public Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request, CancellationToken ct = default) =>
            Task.FromResult(StatusResponse);

        public Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct = default) =>
            Task.FromResult(SubmitResponse);
    }

    #endregion
}
