using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Tests.Repositories.Nutrition;

public class FoodSyncRepositoryTests : IDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task GetLatestSyncLogAsync_NoLogs_ReturnsNull()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        Assert.Null(await repo.GetLatestSyncLogAsync());
    }

    [Fact]
    public async Task AddSyncLogAsync_ValidLog_PersistedWithId()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        var log = new FoodSyncLog { StartedAt = DateTime.UtcNow, Status = "Running", ItemsUpserted = 0, ItemsFailed = 0 };

        await repo.AddSyncLogAsync(log);

        Assert.True(log.Id > 0);
    }

    [Fact]
    public async Task GetLatestSyncLogAsync_MultipleLogs_ReturnsMostRecent()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        await repo.AddSyncLogAsync(new FoodSyncLog { StartedAt = DateTime.UtcNow.AddDays(-7), Status = "Success", ItemsUpserted = 10, ItemsFailed = 0 });
        await repo.AddSyncLogAsync(new FoodSyncLog { StartedAt = DateTime.UtcNow, Status = "Success", ItemsUpserted = 5, ItemsFailed = 0 });

        var result = await repo.GetLatestSyncLogAsync();

        Assert.NotNull(result);
        Assert.Equal(5, result.ItemsUpserted);
    }

    [Fact]
    public async Task AddFoodItemVersionAsync_ValidVersion_PersistedWithId()
    {
        var repo = new FoodSyncRepository(_fixture.Db);
        var item = new FoodItem { Name = "Spinach", Calories = 23, Protein = 2.9f, Carbs = 3.6f, Fats = 0.4f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _fixture.Db.FoodItems.AddAsync(item);
        await _fixture.Db.SaveChangesAsync();

        var version = new FoodItemVersion
        {
            FoodItemId = item.Id,
            NutrientJson = "{\"Calories\":20}",
            VersionedAt = DateTime.UtcNow,
            ChangeSource = "FDC"
        };
        await repo.AddFoodItemVersionAsync(version);

        Assert.True(version.Id > 0);
    }
}