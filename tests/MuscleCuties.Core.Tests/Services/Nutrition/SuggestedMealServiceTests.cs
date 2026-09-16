using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class SuggestedMealServiceTests
{
    private readonly IFoodSyncService _foodSyncService = Substitute.For<IFoodSyncService>();
    private readonly INutritionRepository _nutritionRepository = Substitute.For<INutritionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task SuggestAsync_PrefersFdcProducts()
    {
        var fdcFoods = BuildBalancedFoods(useFdcIds: true);
        _foodSyncService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(fdcFoods);
        ConfigureDefaults();

        var suggestions = await CreateService().SuggestAsync(
            1,
            MealType.Lunch,
            CyclePhase.Follicular,
            DateTime.Today,
            0f);

        Assert.NotEmpty(suggestions);
        Assert.All(
            suggestions.SelectMany(suggestion => suggestion.AllComponents).SelectMany(component => component.Ingredients),
            ingredient => Assert.NotNull(ingredient.Food.FdcId));
        await _foodSyncService.Received().SearchAsync(
            Arg.Any<string>(),
            15,
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestAsync_FallsBackToLocalFoodsWhenApiIsUnavailable()
    {
        ConfigureDefaults();
        _foodSyncService.SearchAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<List<FoodItem>>>(_ => throw new HttpRequestException("Unavailable"));
        _nutritionRepository.GetAllAsync().Returns(BuildBalancedFoods(useFdcIds: false));

        var suggestions = await CreateService().SuggestAsync(
            1,
            MealType.Lunch,
            CyclePhase.Follicular,
            DateTime.Today,
            0f);

        Assert.NotEmpty(suggestions);
        await _nutritionRepository.Received(1).GetAllAsync();
    }

    private SuggestedMealService CreateService()
    {
        return new SuggestedMealService(
            new NutritionPlanner(new CalorieCalculator()),
            _nutritionRepository,
            _foodSyncService,
            _userRepository);
    }

    private void ConfigureDefaults()
    {
        _userRepository.GetProfileAsync(1).Returns((UserProfile?)null);
        _nutritionRepository.GetLoggedMealsByDateRangeAsync(
                1,
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>())
            .Returns([]);
        _nutritionRepository.GetAllAsync().Returns([]);
    }

    private static List<FoodItem> BuildBalancedFoods(bool useFdcIds)
    {
        return
        [
            Food(1, "Chicken breast", 165f, 31f, 0f, 3.6f, useFdcIds),
            Food(2, "Brown rice", 123f, 2.7f, 25.6f, 1f, useFdcIds),
            Food(3, "Spinach", 23f, 2.9f, 3.6f, 0.4f, useFdcIds),
            Food(4, "Olive oil", 884f, 0f, 0f, 100f, useFdcIds)
        ];
    }

    private static FoodItem Food(
        int id,
        string name,
        float calories,
        float protein,
        float carbs,
        float fats,
        bool useFdcId)
    {
        return new FoodItem
        {
            Id = id,
            FdcId = useFdcId ? 1000 + id : null,
            Name = name,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fats = fats
        };
    }
}
