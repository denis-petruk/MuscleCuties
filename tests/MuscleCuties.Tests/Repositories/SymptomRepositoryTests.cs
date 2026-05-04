using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Tests.Repositories;

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
        var cycle = new CycleLog { UserId = user.Id, StartDate = DateTime.UtcNow.AddDays(-5), CycleLength = 0, CreatedAt = DateTime.UtcNow };
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

        await repo.AddAsync(new SymptomLog
        {
            UserId = user.Id,
            CycleLogId = cycle.Id,
            Date = date,
            SymptomType = SymptomType.Cramps,
            Severity = 3,
            CreatedAt = DateTime.UtcNow
        });

        var results = await repo.GetByDateAsync(user.Id, date);
        Assert.Single(results);
    }

    [Fact]
    public async Task GetByCycleAsync_MultipleLogsInCycle_ReturnsAllOrdered()
    {
        var (user, cycle) = await SeedAsync("sym2@test.com");
        var repo = new SymptomRepository(_fixture.Db);
        var baseDate = DateTime.UtcNow.Date;

        await repo.AddAsync(new SymptomLog { UserId = user.Id, CycleLogId = cycle.Id, Date = baseDate.AddDays(-2), SymptomType = SymptomType.Fatigue, Severity = 2, CreatedAt = DateTime.UtcNow });
        await repo.AddAsync(new SymptomLog { UserId = user.Id, CycleLogId = cycle.Id, Date = baseDate.AddDays(-1), SymptomType = SymptomType.Bloating, Severity = 1, CreatedAt = DateTime.UtcNow });

        var results = await repo.GetByCycleAsync(user.Id, cycle.Id);
        Assert.Equal(2, results.Count);
        Assert.True(results[0].Date < results[1].Date);
    }
}