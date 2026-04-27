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
        new CycleService(new CycleRepository(_fixture.Db), new UserRepository(_fixture.Db));

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
    public async Task EndCurrentCycleAsync_SetsCycleEndDate()
    {
        var user = await SeedUserAsync("cycle_svc2@test.com");
        var service = CreateService();
        await service.StartNewCycleAsync(user.Id);

        await service.EndCurrentCycleAsync(user.Id);

        var cycle = await service.GetCurrentCycleAsync(user.Id);
        Assert.NotNull(cycle!.CycleEndDate);
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
    public void CalculateCycleDay_StartDateToday_ReturnsOne()
    {
        var service = CreateService();
        var start = DateTime.UtcNow;

        var day = service.CalculateCycleDay(start);

        Assert.Equal(1, day);
    }

    [Fact]
    public void CalculateCycleDay_StartDateFiveDaysAgo_ReturnsSix()
    {
        var service = CreateService();
        var start = DateTime.UtcNow.AddDays(-5);

        var day = service.CalculateCycleDay(start);

        Assert.Equal(6, day);
    }

    [Theory]
    [InlineData(1, 28, CyclePhase.Menstrual)]
    [InlineData(7, 28, CyclePhase.Follicular)]
    [InlineData(14, 28, CyclePhase.Ovulatory)]
    [InlineData(20, 28, CyclePhase.Luteal)]
    public void CalculatePhase_StandardCycle_ReturnsExpected(int day, int length, CyclePhase expected)
    {
        var service = CreateService();
        Assert.Equal(expected, service.CalculatePhase(day, length));
    }
}
