using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Tests.Repositories.Cycle;

public class CycleRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public CycleRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetLatestCycleAsync_MultipleEntries_ReturnsMostRecent()
    {
        var user = await SeedUserAsync("cycle1@test.com");
        var repo = new CycleRepository(_fixture.Db);

        var older = new CycleLog { UserId = user.Id, StartDate = DateTime.UtcNow.AddDays(-28), CycleLength = 0, CreatedAt = DateTime.UtcNow };
        var newer = new CycleLog { UserId = user.Id, StartDate = DateTime.UtcNow.AddDays(-5), CycleLength = 0, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(older);
        await repo.AddAsync(newer);

        var result = await repo.GetLatestCycleAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(newer.StartDate, result.StartDate);
    }

    [Fact]
    public async Task GetLatestCycleAsync_NoEntries_ReturnsNull()
    {
        var user = await SeedUserAsync("cycle2@test.com");
        var repo = new CycleRepository(_fixture.Db);

        var result = await repo.GetLatestCycleAsync(user.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCycleHistoryAsync_TwoEntries_ReturnsBothDescending()
    {
        var user = await SeedUserAsync("cycle3@test.com");
        var repo = new CycleRepository(_fixture.Db);

        await repo.AddAsync(new CycleLog { UserId = user.Id, StartDate = DateTime.UtcNow.AddDays(-56), CycleLength = 0, CreatedAt = DateTime.UtcNow });
        await repo.AddAsync(new CycleLog { UserId = user.Id, StartDate = DateTime.UtcNow.AddDays(-28), CycleLength = 0, CreatedAt = DateTime.UtcNow });

        var result = await repo.GetCycleHistoryAsync(user.Id);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].StartDate > result[1].StartDate);
    }

    [Fact]
    public async Task GetLatestPhaseLogAsync_MultipleEntries_ReturnsMostRecent()
    {
        var user = await SeedUserAsync("cycle4@test.com");
        var repo = new CycleRepository(_fixture.Db);

        await repo.AddPhaseLogAsync(new CyclePhaseLog
        {
            UserId = user.Id,
            Phase = CyclePhase.Follicular,
            LoggedAt = DateTime.Today.AddDays(-2),
            CreatedAt = DateTime.UtcNow
        });
        await repo.AddPhaseLogAsync(new CyclePhaseLog
        {
            UserId = user.Id,
            Phase = CyclePhase.Luteal,
            LoggedAt = DateTime.Today,
            CreatedAt = DateTime.UtcNow
        });

        var result = await repo.GetLatestPhaseLogAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(CyclePhase.Luteal, result!.Phase);
    }
}
