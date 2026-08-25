using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Cycle;

public class CycleServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public CycleServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private CycleService CreateService() =>
        new CycleService(
            new CycleRepository(_fixture.Db),
            new UserRepository(_fixture.Db),
            new CyclePredictionPlanner(new CyclePhaseCalculator()));

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    private async Task<User> SeedUserWithProfileAsync(string email, CycleTrackingMode trackingMode)
    {
        var user = await SeedUserAsync(email);
        await _fixture.Db.UserProfiles.AddAsync(new UserProfile
        {
            UserId = user.Id,
            Name = "Cycle Tester",
            DateOfBirth = DateTime.Today.AddYears(-28),
            Height = 165,
            Weight = 65,
            Goal = UserGoal.MaintainHealth,
            WeightGoalPace = WeightGoalPace.Steady,
            TrainingExperienceLevel = TrainingExperienceLevel.Beginner,
            CycleTrackingMode = trackingMode,
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            UpdatedAt = DateTime.UtcNow
        });
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task StartNewCycleAsync_CreatesLogForUser()
    {
        var user = await SeedUserAsync("cycle_svc1@test.com");
        var service = CreateService();

        await service.StartNewCycleAsync(user.Id);

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        Assert.NotNull(cycle);
        Assert.Equal(user.Id, cycle.UserId);
    }

    [Fact]
    public async Task EndCurrentCycleAsync_SetsCycleEndDateAndLength()
    {
        var user = await SeedUserAsync("cycle_svc2@test.com");
        var service = CreateService();
        await service.StartNewCycleAsync(user.Id);

        await service.EndCurrentCycleAsync(user.Id);

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        Assert.NotNull(cycle!.EndDate);
        Assert.True(cycle.CycleLength >= 0);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_NoCycle_ReturnsFollicular()
    {
        var user = await SeedUserAsync("cycle_svc3@test.com");
        var service = CreateService();

        var phase = await service.GetCurrentPhaseAsync(user.Id);

        Assert.Equal(CyclePhase.Follicular, phase);
    }

    [Fact]
    public async Task LogPhaseShiftAsync_MenstrualWithoutActiveCycle_StartsNewCycleAndStoresLog()
    {
        var user = await SeedUserAsync("cycle_svc4@test.com");
        var service = CreateService();
        var periodStart = DateTime.Today.AddDays(-1);

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Menstrual, periodStart, "period started");

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        var phaseLog = await service.GetLatestPhaseLogAsync(user.Id);

        Assert.NotNull(cycle);
        Assert.Equal(periodStart.Date, cycle!.StartDate.Date);
        Assert.Null(cycle.EndDate);
        Assert.NotNull(phaseLog);
        Assert.Equal(cycle.Id, phaseLog!.CycleLogId);
        Assert.Equal(CyclePhase.Menstrual, phaseLog.Phase);
        Assert.Equal("period started", phaseLog.Note);
    }

    [Fact]
    public async Task LogPhaseShiftAsync_MenstrualWithActiveCycle_KeepsCycleDayStable()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc8@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();
        await service.StartNewCycleAsync(user.Id);

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Menstrual, DateTime.Today, null);

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        var prediction = await service.GetPredictionAsync(user.Id);

        Assert.Equal(DateTime.Today, cycle!.StartDate.Date);
        Assert.Equal(1, prediction.CurrentDay);
        Assert.Equal(CyclePhase.Menstrual, prediction.CurrentPhase);
    }

    [Fact]
    public async Task LogPhaseShiftAsync_MenstrualWithOlderActiveCycle_StartsNewCycleAnchor()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc10@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();
        await _fixture.Db.CycleLogs.AddAsync(new CycleLog
        {
            UserId = user.Id,
            StartDate = DateTime.Today.AddDays(-22),
            CycleLength = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-22)
        });
        await _fixture.Db.SaveChangesAsync();

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Menstrual, DateTime.Today, "new period");

        var activeCycle = await service.GetCurrentCycleAsync(user.Id);
        var latestLog = await service.GetLatestPhaseLogAsync(user.Id);
        var cycles = await _fixture.Db.CycleLogs
            .Where(cycle => cycle.UserId == user.Id)
            .OrderBy(cycle => cycle.StartDate)
            .ToListAsync();

        Assert.Equal(DateTime.Today, activeCycle!.StartDate.Date);
        Assert.Null(activeCycle.EndDate);
        Assert.Equal(2, cycles.Count);
        Assert.NotNull(cycles[0].EndDate);
        Assert.Equal(activeCycle.Id, latestLog!.CycleLogId);
    }

    [Fact]
    public async Task LogPhaseShiftAsync_Ovulatory_AdjustsPredictionToOvulatory()
    {
        var user = await SeedUserAsync("cycle_svc5@test.com");
        var service = CreateService();

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Ovulatory, DateTime.Today, null);

        var prediction = await service.GetPredictionAsync(user.Id);

        Assert.Equal(CyclePhase.Ovulatory, prediction.CurrentPhase);
        Assert.True(prediction.HasActiveCycle);
        Assert.Equal(14, prediction.CurrentDay);
    }

    [Fact]
    public async Task SetPhaseForDateAsync_ExistingDate_UpdatesSinglePhaseLog()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc9@test.com", CycleTrackingMode.AutomaticPrediction);
        var service = CreateService();
        var loggedAt = DateTime.Today.AddDays(-2);

        await service.SetPhaseForDateAsync(user.Id, CyclePhase.Follicular, loggedAt, "first pick");
        await service.SetPhaseForDateAsync(user.Id, CyclePhase.Ovulatory, loggedAt, "corrected pick");

        var logs = await _fixture.Db.CyclePhaseLogs
            .Where(log => log.UserId == user.Id && log.LoggedAt == loggedAt.Date)
            .ToListAsync();
        var profile = await _fixture.Db.UserProfiles.SingleAsync(profile => profile.UserId == user.Id);

        Assert.Single(logs);
        Assert.Equal(CyclePhase.Ovulatory, logs[0].Phase);
        Assert.Equal("corrected pick", logs[0].Note);
        Assert.Equal(CycleTrackingMode.ManualPhaseLogging, profile.CycleTrackingMode);
    }

    [Fact]
    public async Task SetPhaseForDateAsync_WhenSkippingPreviousPhase_RejectsLog()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc11@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();
        var previousDate = DateTime.Today.AddDays(-2);

        await service.SetPhaseForDateAsync(user.Id, CyclePhase.Menstrual, previousDate, "period");

        var error = await Assert.ThrowsAsync<CyclePhaseOrderException>(() =>
            service.SetPhaseForDateAsync(user.Id, CyclePhase.Ovulatory, previousDate.AddDays(1), "skip"));

        Assert.Contains("Follicular", error.Message);
        var logs = await _fixture.Db.CyclePhaseLogs
            .Where(log => log.UserId == user.Id)
            .ToListAsync();
        Assert.Single(logs);
    }

    [Fact]
    public async Task SetPhaseForDateAsync_WhenPreviousDayAllowsNextPhase_SavesLog()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc13@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();
        var menstrualStart = DateTime.Today.AddDays(-10);

        await service.SetPhaseForDateAsync(user.Id, CyclePhase.Menstrual, menstrualStart, "period");
        await service.SetPhaseForDateAsync(user.Id, CyclePhase.Ovulatory, DateTime.Today, "ovulation");

        var latestLog = await service.GetLatestPhaseLogAsync(user.Id);
        var logs = await _fixture.Db.CyclePhaseLogs
            .Where(log => log.UserId == user.Id)
            .ToListAsync();

        Assert.Equal(CyclePhase.Ovulatory, latestLog!.Phase);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task SetPhaseForDateAsync_WhenBreakingOrderBeforeNextPhase_RejectsLog()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc12@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();
        var cycle = new CycleLog
        {
            UserId = user.Id,
            StartDate = DateTime.Today.AddDays(-12),
            CycleLength = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-12)
        };
        await _fixture.Db.CycleLogs.AddAsync(cycle);
        await _fixture.Db.SaveChangesAsync();

        var nextDate = DateTime.Today.AddDays(2);
        await _fixture.Db.CyclePhaseLogs.AddAsync(new CyclePhaseLog
        {
            UserId = user.Id,
            CycleLogId = cycle.Id,
            Phase = CyclePhase.Luteal,
            LoggedAt = nextDate,
            CreatedAt = DateTime.UtcNow
        });
        await _fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<CyclePhaseOrderException>(() =>
            service.SetPhaseForDateAsync(user.Id, CyclePhase.Follicular, DateTime.Today, "skip before next"));

        Assert.Contains("Ovulatory", error.Message);
        var logs = await _fixture.Db.CyclePhaseLogs
            .Where(log => log.UserId == user.Id)
            .ToListAsync();
        Assert.Single(logs);
    }

    [Fact]
    public async Task LogPhaseShiftAsync_NonMenstrualWithActiveCycle_KeepsCycleDayStable()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc7@test.com", CycleTrackingMode.AutomaticPrediction);
        var service = CreateService();
        await service.StartNewCycleAsync(user.Id);

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Ovulatory, DateTime.Today, null);

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        var prediction = await service.GetPredictionAsync(user.Id);

        Assert.Equal(DateTime.UtcNow.Date, cycle!.StartDate.Date);
        Assert.Equal(1, prediction.CurrentDay);
        Assert.Equal(CyclePhase.Ovulatory, prediction.CurrentPhase);

        var profile = await new UserRepository(_fixture.Db).GetProfileAsync(user.Id);
        Assert.Equal(CycleTrackingMode.ManualPhaseLogging, profile!.CycleTrackingMode);
    }

    [Fact]
    public async Task GetPredictionAsync_ManualPhaseLogFromPast_AdvancesFromLoggedAnchor()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc6@test.com", CycleTrackingMode.ManualPhaseLogging);
        var service = CreateService();

        await service.LogPhaseShiftAsync(user.Id, CyclePhase.Ovulatory, DateTime.Today.AddDays(-10), null);

        var prediction = await service.GetPredictionAsync(user.Id);

        Assert.Equal(24, prediction.CurrentDay);
        Assert.Equal(CyclePhase.Luteal, prediction.CurrentPhase);
        Assert.Equal("manual phase log", prediction.PredictionSource);
    }

    [Fact]
    public async Task GetPredictionAsync_FuturePhaseLogDoesNotOverrideTodayLog()
    {
        var user = await SeedUserWithProfileAsync("cycle_svc14@test.com", CycleTrackingMode.ManualPhaseLogging);
        var cycle = new CycleLog
        {
            UserId = user.Id,
            StartDate = DateTime.Today.AddDays(-10),
            CycleLength = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        await _fixture.Db.CycleLogs.AddAsync(cycle);
        await _fixture.Db.SaveChangesAsync();
        await _fixture.Db.CyclePhaseLogs.AddRangeAsync(
            new CyclePhaseLog
            {
                UserId = user.Id,
                CycleLogId = cycle.Id,
                Phase = CyclePhase.Follicular,
                LoggedAt = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            },
            new CyclePhaseLog
            {
                UserId = user.Id,
                CycleLogId = cycle.Id,
                Phase = CyclePhase.Luteal,
                LoggedAt = DateTime.Today.AddDays(2),
                CreatedAt = DateTime.UtcNow.AddMinutes(1)
            });
        await _fixture.Db.SaveChangesAsync();
        var service = CreateService();

        var prediction = await service.GetPredictionAsync(user.Id);

        Assert.Equal(CyclePhase.Follicular, prediction.CurrentPhase);
        Assert.Equal("manual phase log", prediction.PredictionSource);
    }
}
