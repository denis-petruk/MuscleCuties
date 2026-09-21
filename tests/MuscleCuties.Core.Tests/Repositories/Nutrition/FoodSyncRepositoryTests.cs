using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;

namespace MuscleCuties.Core.Tests.Repositories.Nutrition;

public class FoodSyncRepositoryTests : IDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task AddSyncLogAsync_ValidLog_PersistedWithId()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        var log = new FoodSyncLog
        { StartedAt = DateTime.UtcNow, Status = "Running", ItemsUpserted = 0, ItemsFailed = 0 };

        await repo.AddSyncLogAsync(log);

        Assert.True(log.Id > 0);
    }

    [Fact]
    public async Task UpdateSyncLogAsync_UpdatesPersistedStatus()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        var log = new FoodSyncLog
        { StartedAt = DateTime.UtcNow, Status = "Running", ItemsUpserted = 0, ItemsFailed = 0 };
        await repo.AddSyncLogAsync(log);

        log.Status = "Success";
        log.ItemsUpserted = 5;
        await repo.UpdateSyncLogAsync(log);

        var result = await _fixture.Db.FoodSyncLogs.AsNoTracking().SingleAsync();
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.Equal(5, result.ItemsUpserted);
    }

    [Fact]
    public async Task AddFoodItemVersionsAsync_ValidVersion_PersistedWithId()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        var item = new FoodItem
        {
            Name = "Spinach",
            Calories = 23,
            Protein = 2.9f,
            Carbs = 3.6f,
            Fats = 0.4f,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _fixture.Db.FoodItems.AddAsync(item);
        await _fixture.Db.SaveChangesAsync();

        var version = new FoodItemVersion
        {
            FoodItemId = item.Id,
            NutrientJson = "{\"Calories\":20}",
            VersionedAt = DateTime.UtcNow,
            ChangeSource = "FDC"
        };
        await repo.AddFoodItemVersionsAsync([version]);

        Assert.True(version.Id > 0);
    }
}
