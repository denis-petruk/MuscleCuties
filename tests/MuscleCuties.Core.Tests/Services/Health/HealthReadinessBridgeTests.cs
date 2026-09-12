using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Services.Health;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Services.Health;

public class HealthReadinessBridgeTests
{
    private readonly IHealthSyncService _healthSync = Substitute.For<IHealthSyncService>();
    private readonly HealthReadinessBridge _bridge;

    public HealthReadinessBridgeTests()
    {
        _bridge = new HealthReadinessBridge(_healthSync);
    }

    private static DailyReadinessLog MakeLog() => new()
    {
        UserId = 1,
        Date = DateOnly.FromDateTime(DateTime.Today),
        SleepHours = 7.0,
        Sleep3dAvg = 7.0,
        StepsYesterday = 8000,
        Steps7dAvg = 8000,
        Energy = 3,
        Pain = 0,
        Bloating = false,
        ReadinessScore = 0,
        ReadinessTier = 0,
        Phase = string.Empty
    };

    [Fact]
    public async Task Enrich_NullSummary_LogUnchanged()
    {
        _healthSync.GetCachedWeeklySummaryAsync(1).Returns((HealthWeeklySummary?)null);

        var log = MakeLog();
        await _bridge.EnrichAsync(1, log);

        Assert.Equal(8000, log.StepsYesterday);
        Assert.Equal(8000, log.Steps7dAvg);
        Assert.Equal(7.0, log.Sleep3dAvg);
    }

    [Fact]
    public async Task Enrich_WithMovementData_UpdatesSteps()
    {
        var summary = new HealthWeeklySummary(
            HealthDataSource.AppleHealth,
            DateTime.Today.AddDays(-7), DateTime.Today,
            12500, 0, 0, 0, 0, DateTime.UtcNow);

        _healthSync.GetCachedWeeklySummaryAsync(1).Returns(summary);

        var log = MakeLog();
        await _bridge.EnrichAsync(1, log);

        Assert.Equal(12500, log.StepsYesterday);
        Assert.Equal(12500, log.Steps7dAvg);
        Assert.Equal(7.0, log.Sleep3dAvg);
    }

    [Fact]
    public async Task Enrich_WithSleepData_UpdatesSleep()
    {
        var summary = new HealthWeeklySummary(
            HealthDataSource.AppleHealth,
            DateTime.Today.AddDays(-7), DateTime.Today,
            0, 6.5, 72, 0, 0, DateTime.UtcNow);

        _healthSync.GetCachedWeeklySummaryAsync(1).Returns(summary);

        var log = MakeLog();
        await _bridge.EnrichAsync(1, log);

        Assert.Equal(6.5, log.Sleep3dAvg);
        Assert.Equal(8000, log.StepsYesterday);
    }

    [Fact]
    public async Task Enrich_WithBothData_UpdatesBoth()
    {
        var summary = new HealthWeeklySummary(
            HealthDataSource.AppleHealth,
            DateTime.Today.AddDays(-7), DateTime.Today,
            10000, 7.2, 84, 62, 45, DateTime.UtcNow);

        _healthSync.GetCachedWeeklySummaryAsync(1).Returns(summary);

        var log = MakeLog();
        await _bridge.EnrichAsync(1, log);

        Assert.Equal(10000, log.StepsYesterday);
        Assert.Equal(10000, log.Steps7dAvg);
        Assert.Equal(7.2, log.Sleep3dAvg);
    }
}
