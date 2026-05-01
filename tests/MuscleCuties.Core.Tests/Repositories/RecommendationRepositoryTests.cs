using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Tests.Repositories;

public class RecommendationRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public RecommendationRepositoryTests(DatabaseFixture fixture)
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
    public async Task GetSetByDateAsync_NoSet_ReturnsNull()
    {
        var user = await SeedUserAsync("rec1@test.com");
        var repo = new RecommendationRepository(_fixture.Db);

        var result = await repo.GetSetByDateAsync(user.Id, DateTime.UtcNow.Date);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ValidSet_PersistedWithId()
    {
        var user = await SeedUserAsync("rec2@test.com");
        var repo = new RecommendationRepository(_fixture.Db);

        var set = new RecommendationSet
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            CyclePhase = CyclePhase.Follicular,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        await repo.AddAsync(set);

        Assert.True(set.Id > 0);
    }

    [Fact]
    public async Task GetSetByDateAsync_ExistingSet_ReturnsIt()
    {
        var user = await SeedUserAsync("rec3@test.com");
        var repo = new RecommendationRepository(_fixture.Db);
        var today = DateTime.UtcNow.Date;

        await repo.AddAsync(new RecommendationSet
        {
            UserId = user.Id,
            Date = today,
            CyclePhase = CyclePhase.Luteal,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        var result = await repo.GetSetByDateAsync(user.Id, today);

        Assert.NotNull(result);
        Assert.Equal(CyclePhase.Luteal, result.CyclePhase);
    }

    [Fact]
    public async Task AddWellnessRecommendationAsync_ValidRec_PersistedWithId()
    {
        var user = await SeedUserAsync("rec4@test.com");
        var repo = new RecommendationRepository(_fixture.Db);

        var set = new RecommendationSet
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.Date,
            CyclePhase = CyclePhase.Menstrual,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        await repo.AddAsync(set);

        var rec = new WellnessRecommendation
        {
            RecommendationSetId = set.Id,
            Category = "Rest",
            Notes = "Take it easy today."
        };
        await repo.AddWellnessRecommendationAsync(rec);

        Assert.True(rec.Id > 0);
    }
}