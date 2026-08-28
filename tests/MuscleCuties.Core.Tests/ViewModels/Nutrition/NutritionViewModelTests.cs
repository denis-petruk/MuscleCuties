using NSubstitute;
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
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Inputs;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Nutrition;

public class NutritionViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();

    private NutritionViewModel CreateViewModel() =>
        new(_authService, _cycleService, _nutritionService);

    private void ConfigureLoadData(
        CyclePhase phase = CyclePhase.Ovulatory,
        float consumedCalories = 900f,
        (float Protein, float Carbs, float Fats)? consumedMacros = null)
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(phase);
        _nutritionService.CalculateDailyTargetsAsync(1, phase)
            .Returns((1800f, 130f, 180f, 60f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(consumedCalories);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>())
            .Returns(consumedMacros ?? (50f, 90f, 30f));
        _nutritionService.GetLoggedMealsByDateAsync(1, Arg.Any<DateTime>())
            .Returns(BuildLoggedMeals(consumedCalories, consumedMacros ?? (50f, 90f, 30f)));
    }

    private static List<LoggedMeal> BuildLoggedMeals(
        float calories,
        (float Protein, float Carbs, float Fats) macros)
    {
        if (calories <= 0f && macros is { Protein: <= 0f, Carbs: <= 0f, Fats: <= 0f })
            return [];

        return
        [
            new LoggedMeal
            {
                Id = 1,
                LoggedAt = DateTime.Today.AddHours(8),
                MealType = MealType.Breakfast,
                Entries =
                [
                    new LoggedMealEntry
                    {
                        Grams = 100f,
                        FoodItem = new FoodItem
                        {
                            Name = "Loaded meal",
                            Calories = calories,
                            Protein = macros.Protein,
                            Carbs = macros.Carbs,
                            Fats = macros.Fats,
                            Fiber = 5f,
                            Potassium = 300f
                        }
                    }
                ]
            }
        ];
    }

    [Fact]
    public async Task LoadData_SetsTargetsAndConsumed()
    {
        ConfigureLoadData();

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1800f, vm.TargetCalories);
        Assert.Equal(130f, vm.TargetProtein);
        Assert.Equal(180f, vm.TargetCarbs);
        Assert.Equal(60f, vm.TargetFats);
        Assert.Equal(900f, vm.ConsumedCalories);
        Assert.Equal(50f, vm.ConsumedProtein);
        Assert.Equal(90f, vm.ConsumedCarbs);
        Assert.Equal(30f, vm.ConsumedFats);
        Assert.Equal(CyclePhase.Ovulatory, vm.CurrentPhase);
        Assert.Equal("PHASE FOCUS · OVULATORY", vm.PhaseFocusBadgeText);
        Assert.Equal("Peak-performance plate", vm.PhaseFocusTitle);
        Assert.Contains("hydration", vm.PhaseFocusCopy);
    }

    [Fact]
    public async Task LoadData_CaloriesProgressClampedToOne()
    {
        ConfigureLoadData(CyclePhase.Menstrual, 3000f, (0f, 0f, 0f));
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Menstrual)
            .Returns((2000f, 150f, 200f, 70f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1f, vm.CaloriesProgress);
    }

    [Fact]
    public async Task SearchFoodCommand_PopulatesSearchResults()
    {
        _nutritionService.SearchFoodItemsAsync("carrot", 15, 1)
            .Returns(
            [
                new FoodItem
                {
                    Id = 10,
                    Name = "Carrot, raw",
                    Calories = 41f,
                    Protein = 0.9f,
                    Carbs = 9.6f,
                    Fats = 0.2f,
                    DataType = "Branded",
                    BrandOwner = "Crunch Farm",
                    GtinUpc = "123456789",
                    ServingSize = 100f,
                    ServingSizeUnit = "g"
                }
            ]);

        var vm = CreateViewModel();
        vm.SearchQuery = "carrot";

        await vm.SearchFoodCommand.ExecuteAsync(null);

        var result = Assert.Single(vm.FoodSearchResults);
        Assert.Equal(10, result.FoodItemId);
        Assert.Equal("Carrot, raw", result.Name);
        Assert.Equal(41f, result.Calories);
        Assert.Equal(0.9f, result.Protein);
        Assert.Equal(9.6f, result.Carbs);
        Assert.Equal(0.2f, result.Fats);
        Assert.Equal("PRODUCTS (1)", vm.FoodSearchResultsTitle);
        Assert.Contains("Branded", result.SourceSummary);
        Assert.Contains("Crunch Farm", result.SourceSummary);
        Assert.Contains("UPC 123456789", result.SourceSummary);
        Assert.Contains("1 serving: 41 kcal", result.NutritionSummary);
        Assert.False(vm.ShowBrowseMoreFoods);
    }

    [Fact]
    public async Task BrowseMoreFoodsCommand_AppendsNextSearchPage()
    {
        var firstPage = Enumerable.Range(1, 15)
            .Select(i => new FoodItem
            {
                Id = i,
                Name = $"Oats {i}",
                Calories = 100f + i,
                Protein = 10f,
                Carbs = 20f,
                Fats = 5f
            })
            .ToList();

        var secondPage = Enumerable.Range(16, 2)
            .Select(i => new FoodItem
            {
                Id = i,
                Name = $"Oats {i}",
                Calories = 100f + i,
                Protein = 10f,
                Carbs = 20f,
                Fats = 5f
            })
            .ToList();

        _nutritionService.SearchFoodItemsAsync("oats", 15, 1).Returns(firstPage);
        _nutritionService.SearchFoodItemsAsync("oats", 15, 2).Returns(secondPage);

        var vm = CreateViewModel();
        vm.SearchQuery = "oats";

        await vm.SearchFoodCommand.ExecuteAsync(null);
        Assert.Equal(15, vm.FoodSearchResults.Count);
        Assert.True(vm.ShowBrowseMoreFoods);

        await vm.BrowseMoreFoodsCommand.ExecuteAsync(null);

        Assert.Equal(17, vm.FoodSearchResults.Count);
        Assert.Contains(vm.FoodSearchResults, item => item.Name == "Oats 1");
        Assert.Contains(vm.FoodSearchResults, item => item.Name == "Oats 16");
        Assert.False(vm.ShowBrowseMoreFoods);
    }

    [Fact]
    public async Task LogMealCommand_LogsMealIngredientsAtSelectedTimeAndReloadsMeals()
    {
        ConfigureLoadData();

        var selectedTime = new TimeSpan(12, 30, 0);
        var vm = CreateViewModel();
        vm.IsAddFoodPanelVisible = true;
        vm.SelectedFoodResult = new FoodSearchResultItem { FoodItemId = 10, Name = "Olive oil", Calories = 884f, Fats = 100f };
        vm.FoodGrams = "10";
        vm.SelectedMealType = MealType.Snack;
        vm.SelectedMealTime = selectedTime;

        vm.AddIngredientCommand.Execute(null);
        await vm.LogMealCommand.ExecuteAsync(null);

        await _nutritionService.Received(1).LogMealAsync(
            1,
            Arg.Is<IReadOnlyCollection<MealIngredientInput>>(items =>
                items.Count == 1 &&
                items.Single().FoodItemId == 10 &&
                items.Single().Grams == 10f),
            MealType.Snack,
            Arg.Is<DateTime>(d => d.Date == DateTime.Today && d.TimeOfDay == selectedTime));
        Assert.False(vm.IsAddFoodPanelVisible);
        Assert.Empty(vm.MealIngredients);
        Assert.True(vm.HasMeals);
        Assert.False(vm.HasNoMeals);
        Assert.Contains("Loaded meal", Assert.Single(vm.Meals).Name);
    }

    [Fact]
    public async Task LogMealCommand_WhenLoggingRejected_ShowsMessage()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _nutritionService
            .LogMealAsync(
                1,
                Arg.Any<IReadOnlyCollection<MealIngredientInput>>(),
                MealType.Snack,
                Arg.Any<DateTime>())
            .Returns(Task.FromException(new InvalidOperationException("Current user no longer exists. Please sign in again.")));

        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem { FoodItemId = 10, Name = "Carrot", Calories = 41f };
        vm.FoodGrams = "10";
        vm.SelectedMealType = MealType.Snack;

        vm.AddIngredientCommand.Execute(null);
        await vm.LogMealCommand.ExecuteAsync(null);

        Assert.Equal("Current user no longer exists. Please sign in again.", vm.AddFoodMessage);
    }

    [Fact]
    public void AddIngredientCommand_MergesSameIngredientIntoDraftMeal()
    {
        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem { FoodItemId = 10, Name = "Oats", Calories = 389f, Protein = 16.9f, Carbs = 66.3f, Fats = 6.9f };
        vm.FoodGrams = "40";
        vm.AddIngredientCommand.Execute(null);

        vm.SelectedFoodResult = new FoodSearchResultItem { FoodItemId = 10, Name = "Oats", Calories = 389f, Protein = 16.9f, Carbs = 66.3f, Fats = 6.9f };
        vm.FoodGrams = "20";
        vm.AddIngredientCommand.Execute(null);

        var ingredient = Assert.Single(vm.MealIngredients);
        Assert.Equal(60f, ingredient.Grams);
        Assert.Contains("60 g", ingredient.AmountSummary);
        Assert.Contains("233 kcal", ingredient.AmountSummary);
    }

    [Fact]
    public void SelectedFoodAmountText_CalculatesMacrosForSelectedServingAmount()
    {
        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem
        {
            FoodItemId = 10,
            Name = "Olive oil",
            Calories = 884f,
            Protein = 0f,
            Carbs = 0f,
            Fats = 100f
        };
        vm.FoodGrams = "10";

        Assert.Equal("10", vm.FoodGrams);
        Assert.Contains("100 g: 884 kcal", vm.SelectedFoodServingText);
        Assert.Contains("10 g: 88 kcal", vm.SelectedFoodAmountText);
        Assert.Contains("F 10.0g", vm.SelectedFoodAmountText);
    }

    [Fact]
    public void SelectedFoodResult_DefaultsAmountFromProductServing()
    {
        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem
        {
            FoodItemId = 10,
            Name = "Oats",
            Calories = 389f,
            Protein = 16.9f,
            Carbs = 66.3f,
            Fats = 6.9f,
            ServingSize = 40f,
            ServingSizeUnit = "g"
        };

        Assert.Equal("serving", vm.SelectedServingOption?.Label);
        Assert.Equal("1", vm.FoodGrams);
        Assert.Contains("1 serving", vm.SelectedFoodAmountText);

        vm.SelectedServingOption = vm.ServingOptions.Single(option => option.Label == "g");

        Assert.Equal("40", vm.FoodGrams);
        Assert.Contains("40 g", vm.SelectedFoodAmountText);
    }

    [Fact]
    public void AddIngredientCommand_UsesSelectedFdcServing()
    {
        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem
        {
            FoodItemId = 10,
            Name = "Protein bar",
            Calories = 400f,
            Protein = 40f,
            Carbs = 40f,
            Fats = 10f,
            ServingSize = 50f,
            ServingSizeUnit = "g"
        };
        Assert.Equal("1", vm.FoodGrams);
        vm.FoodGrams = "2";

        vm.AddIngredientCommand.Execute(null);

        var ingredient = Assert.Single(vm.MealIngredients);
        Assert.Equal(100f, ingredient.Grams);
        Assert.Contains("2 serving", ingredient.AmountSummary);
        Assert.Contains("400 kcal", ingredient.AmountSummary);
    }

    [Fact]
    public void SelectedFoodAmountText_WhenNutritionMissing_ShowsUnavailableMessage()
    {
        var vm = CreateViewModel();
        vm.SelectedFoodResult = new FoodSearchResultItem
        {
            FoodItemId = 10,
            Name = "Unknown food"
        };
        vm.FoodGrams = "100";

        Assert.Contains("Nutrition values are unavailable", vm.SelectedFoodServingText);
        Assert.Contains("Nutrition values are unavailable", vm.SelectedFoodAmountText);
    }

    [Fact]
    public async Task CreateCustomFoodCommand_SavesAndSelectsCustomFood()
    {
        _nutritionService.CreateCustomFoodAsync(Arg.Any<CustomFoodInput>())
            .Returns(new FoodItem
            {
                Id = 99,
                Name = "Protein pancake",
                Calories = 220f,
                Protein = 20f,
                Carbs = 24f,
                Fats = 6f,
                ServingSize = 100f,
                ServingSizeUnit = "g",
                DataType = "Custom",
                IsCustom = true
            });

        var vm = CreateViewModel();
        vm.IsCustomFoodPanelVisible = true;
        vm.CustomFoodName = "Protein pancake";
        vm.CustomFoodCalories = "220";
        vm.CustomFoodProtein = "20";
        vm.CustomFoodCarbs = "24";
        vm.CustomFoodFats = "6";
        vm.CustomFoodServingAmount = "100";
        vm.SelectedCustomFoodServingUnit = "g";

        await vm.CreateCustomFoodCommand.ExecuteAsync(null);

        Assert.Equal(99, vm.SelectedFoodResult?.FoodItemId);
        Assert.False(vm.IsCustomFoodPanelVisible);
        Assert.Equal("1", vm.FoodGrams);
        Assert.Contains("saved", vm.AddFoodMessage);
        await _nutritionService.Received(1).CreateCustomFoodAsync(
            Arg.Is<CustomFoodInput>(input =>
                input.Name == "Protein pancake" &&
                input.Calories == 220f &&
                input.ServingAmount == 100f &&
                input.ServingUnit == "g"));
    }

    [Fact]
    public async Task LoadData_PopulatesMealsWithLoggedTimes()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Ovulatory);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Ovulatory)
            .Returns((1800f, 130f, 180f, 60f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(45f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0f, 0f, 5f));
        _nutritionService.GetLoggedMealsByDateAsync(1, Arg.Any<DateTime>())
            .Returns(
            [
                new LoggedMeal
                {
                    LoggedAt = DateTime.Today.AddHours(12).AddMinutes(30),
                    MealType = MealType.Snack,
                    Entries =
                    [
                        new LoggedMealEntry
                        {
                            Grams = 10,
                            FoodItem = new FoodItem
                            {
                                Name = "Olive oil",
                                Calories = 884f,
                                Fats = 100f,
                            }
                        }
                    ]
                }
            ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        var meal = Assert.Single(vm.Meals);
        Assert.Contains("12:30", meal.Time);
        Assert.Contains("Olive oil", meal.Name);
        Assert.Equal("88 kcal", meal.CaloriesText);
        Assert.Contains("F 10.0g", meal.MacrosText);
        Assert.Equal("0.0g fiber", meal.FiberText);
    }

    [Fact]
    public async Task LoadData_PopulatesMicronutrientProgressFromLoggedFoods()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((1800f, 130f, 180f, 60f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(41f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0.9f, 9.6f, 0.2f));
        _nutritionService.GetLoggedMealsByDateAsync(1, Arg.Any<DateTime>())
            .Returns(
            [
                new LoggedMeal
                {
                    LoggedAt = DateTime.Today.AddHours(9),
                    MealType = MealType.Breakfast,
                    Entries =
                    [
                        new LoggedMealEntry
                        {
                            Grams = 100f,
                            FoodItem = new FoodItem
                            {
                                Name = "Carrot, raw",
                                Calories = 41f,
                                Fiber = 2.8f,
                                VitaminA = 835f,
                                VitaminC = 5.9f,
                                Calcium = 33f,
                                Iron = 0.3f,
                                Magnesium = 12f,
                                Zinc = 0.2f,
                                Potassium = 320f
                            }
                        }
                    ]
                }
            ]);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(12, vm.Micronutrients.Count);
        Assert.Contains(vm.Micronutrients, item =>
            item.Name == "Fiber" &&
            item.Amount == 2.8f &&
            item.Goal == 25f);
        Assert.Contains(vm.Micronutrients, item =>
            item.Name == "Potassium" &&
            item.Amount == 320f &&
            item.Goal == 2600f);
        Assert.Contains(vm.Micronutrients, item =>
            item.Name == "Vitamin A" &&
            item.Amount == 0f &&
            !item.IsGoalHit);
        Assert.Contains("0 of 12", vm.MicronutrientSummaryText);
        Assert.Equal("2.8g fiber", vm.DayFiberText);
    }

    [Fact]
    public async Task OpenMicronutrientsModalCommand_ShowsWholeDayBreakdown()
    {
        ConfigureLoadData(CyclePhase.Follicular, 500f, (30f, 50f, 12f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        vm.OpenMicronutrientsModalCommand.Execute(null);

        Assert.True(vm.IsBreakdownModalVisible);
        Assert.Equal("Daily nutrition", vm.SelectedBreakdownTitle);
        Assert.Equal("500 kcal", vm.SelectedBreakdownCaloriesText);
        Assert.Equal(120f, vm.SelectedBreakdownProteinCalories);
        Assert.Equal(200f, vm.SelectedBreakdownCarbsCalories);
        Assert.Equal(108f, vm.SelectedBreakdownFatsCalories);
        Assert.Equal(3, vm.SelectedBreakdownMacroItems.Count);
        Assert.Equal(12, vm.SelectedBreakdownMicronutrients.Count);
        Assert.Equal("5.0g fiber", vm.SelectedBreakdownFiberText);
    }

    [Fact]
    public async Task OpenMealBreakdownCommand_ShowsSelectedMealBreakdown()
    {
        ConfigureLoadData(CyclePhase.Follicular, 300f, (20f, 40f, 6f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        vm.OpenMealBreakdownCommand.Execute(vm.Meals.Single());

        Assert.True(vm.IsBreakdownModalVisible);
        Assert.Equal("BREAKFAST breakdown", vm.SelectedBreakdownTitle);
        Assert.Contains("Loaded meal", vm.SelectedBreakdownSubtitle);
        Assert.Equal("300 kcal", vm.SelectedBreakdownCaloriesText);
        Assert.Equal("P 20.0g · C 40.0g · F 6.0g", vm.SelectedBreakdownMacrosText);
    }

    [Fact]
    public void SelectedMealTimeText_FormatsSelectedTime()
    {
        var vm = CreateViewModel();

        vm.SelectedMealTime = new TimeSpan(14, 5, 0);

        Assert.Contains("2:05", vm.SelectedMealTimeText);
    }
}
