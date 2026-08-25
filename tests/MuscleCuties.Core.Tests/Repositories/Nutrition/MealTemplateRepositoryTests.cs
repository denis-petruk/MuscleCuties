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

namespace MuscleCuties.Core.Tests.Repositories.Nutrition;

public class MealTemplateRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public MealTemplateRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSystemTemplatesAsync_ReturnsOnlySystemTemplates()
    {
        var repo = new MealTemplateRepository(_fixture.Db);
        await repo.AddAsync(new MealTemplate { Name = "System Meal", MealType = MealType.Breakfast, IsSystem = true, CreatedAt = DateTime.UtcNow });
        await repo.AddAsync(new MealTemplate { UserId = 1, Name = "User Meal", MealType = MealType.Lunch, IsSystem = false, CreatedAt = DateTime.UtcNow });

        var results = await repo.GetSystemTemplatesAsync();

        Assert.All(results, t => Assert.True(t.IsSystem));
    }

    [Fact]
    public async Task GetUserTemplatesAsync_ReturnsOnlyForThatUser()
    {
        var repo = new MealTemplateRepository(_fixture.Db);
        await repo.AddAsync(new MealTemplate { UserId = 1, Name = "User 1 Meal", MealType = MealType.Dinner, IsSystem = false, CreatedAt = DateTime.UtcNow });

        var results = await repo.GetUserTemplatesAsync(1);

        Assert.All(results, t => Assert.Equal(1, t.UserId));
    }

    [Fact]
    public async Task GetTemplateWithEntriesAsync_ReturnsEntriesLoaded()
    {
        var repo = new MealTemplateRepository(_fixture.Db);
        var item = new FoodItem { Name = "Rice", Calories = 130, Protein = 3, Carbs = 28, Fats = 0.3f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await _fixture.Db.FoodItems.AddAsync(item);
        await _fixture.Db.SaveChangesAsync();

        var template = new MealTemplate
        {
            Name = "Rice Bowl",
            MealType = MealType.Lunch,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
            Entries = [new MealTemplateEntry { FoodItemId = item.Id, Grams = 200 }]
        };
        await repo.AddAsync(template);

        var result = await repo.GetTemplateWithEntriesAsync(template.Id);

        Assert.NotNull(result);
        Assert.Single(result.Entries);
    }
}