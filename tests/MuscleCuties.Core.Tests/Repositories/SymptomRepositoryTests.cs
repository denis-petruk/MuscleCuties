using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Tests.Repositories;

public class SymptomRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public SymptomRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(User user, CycleLog cycle)> SeedAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        var cycle = new CycleLog { UserId = user.Id, CycleStartDate = DateTime.UtcNow.AddDays(-5), CycleLength = 28, PeriodLength = 5 };
        await _fixture.Db.CycleLogs.AddAsync(cycle);
        await _fixture.Db.SaveChangesAsync();
        return (user, cycle);
    }

    [Fact]
    public async Task GetByDateAsync_LogOnThatDate_ReturnsIt()
    {
        var (user, cycle) = await SeedAsync("sym1@test.com");
        var repo = new SymptomRepository(_fixture.Db);
        var date = DateTime.UtcNow.Date;

        await repo.AddAsync(new SymptomLog { UserId = user.Id, CycleLogId = cycle.Id, Date = date, Phase = CyclePhase.Menstrual, Pain = 2, Energy = 3 });

        var results = await repo.GetByDateAsync(user.Id, date);
        Assert.Single(results);
    }

    [Fact]
    public async Task GetByCycleAsync_MultipleLogsInCycle_ReturnsAllOrdered()
    {
        var (user, cycle) = await SeedAsync("sym2@test.com");
        var repo = new SymptomRepository(_fixture.Db);
        var base_date = DateTime.UtcNow.Date;

        await repo.AddAsync(new SymptomLog { UserId = user.Id, CycleLogId = cycle.Id, Date = base_date.AddDays(-2), Phase = CyclePhase.Menstrual });
        await repo.AddAsync(new SymptomLog { UserId = user.Id, CycleLogId = cycle.Id, Date = base_date.AddDays(-1), Phase = CyclePhase.Menstrual });

        var results = await repo.GetByCycleAsync(user.Id, cycle.Id);
        Assert.Equal(2, results.Count);
        Assert.True(results[0].Date < results[1].Date);
    }
}
