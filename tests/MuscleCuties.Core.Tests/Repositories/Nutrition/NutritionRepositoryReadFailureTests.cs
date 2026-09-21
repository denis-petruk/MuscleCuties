using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.ViewModels.Common;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Repositories.Nutrition;

public class NutritionRepositoryReadFailureTests
{
    [Fact]
    public async Task MissingFoodColumn_IsReportedByLoadGate_AndCanBeRetriedAfterRepair()
    {
        using var fixture = new DatabaseFixture();
        var food = new FoodItem { Name = "Oats", Calories = 389, Protein = 17 };
        fixture.Db.FoodItems.Add(food);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        await fixture.Db.Database.ExecuteSqlRawAsync("ALTER TABLE FoodItems DROP COLUMN BrandName");

        var repository = new NutritionRepository(fixture.Db);
        var gate = new ViewModelLoadGate(TimeSpan.FromMinutes(2));
        var page = Substitute.For<IPageLoadAware>();
        List<FoodItem>? foods = null;
        async Task LoadAsync() => foods = await DataLoadScheduler.RunAsync(() =>
            repository.GetFoodItemsByIdsAsync([food.Id]));

        await gate.RunAsync(LoadAsync, page);

        Assert.True(page.IsLoadError);
        Assert.False(gate.HasLoaded);
        Assert.Null(foods);

        await fixture.Db.Database.ExecuteSqlRawAsync("ALTER TABLE FoodItems ADD COLUMN BrandName TEXT NULL");
        await gate.RunAsync(LoadAsync, page);

        Assert.False(page.IsLoadError);
        Assert.True(gate.HasLoaded);
        var result = Assert.Single(foods!);
        Assert.Equal(food.Id, result.Id);
        Assert.Equal("Oats", result.Name);
        Assert.Equal(389, result.Calories);
    }

    [Fact]
    public async Task MissingMealTable_DoesNotMasqueradeAsEmptyHistory()
    {
        using var fixture = new DatabaseFixture();
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TABLE LoggedMealEntries");
        var repository = new NutritionRepository(fixture.Db);

        await Assert.ThrowsAsync<SqliteException>(() => repository.GetLoggedMealsByDateAsync(1, DateTime.Today));
        await Assert.ThrowsAsync<SqliteException>(() => repository.GetLoggedMealAsync(1, 1));
    }
}
