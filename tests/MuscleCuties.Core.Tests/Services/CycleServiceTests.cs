using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.Tests.Services;

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
            new CyclePhaseCalculator());

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
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
}