using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Nutrition.Inputs;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition;

public class NutritionService : INutritionService
{
    private readonly IUserRepository _userRepository;
    private readonly INutritionRepository _nutritionRepository;
    private readonly IMealTemplateRepository? _mealTemplateRepository;
    private readonly INutritionPlanner _nutritionPlanner;
    private readonly IFoodSyncService? _foodSyncService;

    public NutritionService(
        IUserRepository userRepository,
        INutritionRepository nutritionRepository,
        ICalorieCalculator calorieCalculator,
        IFoodSyncService? foodSyncService = null,
        IMealTemplateRepository? mealTemplateRepository = null,
        INutritionPlanner? nutritionPlanner = null)
    {
        _userRepository = userRepository;
        _nutritionRepository = nutritionRepository;
        _mealTemplateRepository = mealTemplateRepository;
        _nutritionPlanner = nutritionPlanner ?? new NutritionPlanner(calorieCalculator);
        _foodSyncService = foodSyncService;
    }

    public async Task<NutritionPlan> GetDailyPlanAsync(int userId, CyclePhase phase, DateTime date)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        return profile is null
            ? _nutritionPlanner.CreateFallbackPlan(phase)
            : _nutritionPlanner.CreateDailyPlan(profile, phase, date);
    }

    public async Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId, CyclePhase phase)
    {
        var plan = await GetDailyPlanAsync(userId, phase, DateTime.UtcNow);
        return (plan.Calories, plan.Protein, plan.Carbs, plan.Fats);
    }

    public async Task<MacroNutrients> GetConsumedTotalsAsync(int userId, DateTime date)
    {
        var meals = await _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
        return MacroNutrients.SumMealEntries(meals.SelectMany(m => m.Entries));
    }

    public async Task<float> GetConsumedCaloriesAsync(int userId, DateTime date)
    {
        var totals = await GetConsumedTotalsAsync(userId, date);
        return totals.Calories;
    }

    public async Task<(float Protein, float Carbs, float Fats)> GetConsumedMacrosAsync(int userId, DateTime date)
    {
        var totals = await GetConsumedTotalsAsync(userId, date);
        return (totals.Protein, totals.Carbs, totals.Fats);
    }

    public async Task<List<FoodItem>> SearchFoodItemsAsync(
        string query,
        int pageSize = 15,
        int pageNumber = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        pageSize = Math.Clamp(pageSize, 1, 50);
        pageNumber = Math.Max(1, pageNumber);

        List<FoodItem> foods;
        if (_foodSyncService is null)
        {
            var local = FoodSearchResultFilter.PrepareFoodItems(
                query,
                await _nutritionRepository.SearchFoodItemsAsync(query));

            return local
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            try
            {
                foods = await _foodSyncService.SearchAsync(query, pageSize, pageNumber);
            }
            catch (InvalidOperationException)
            {
                var local = FoodSearchResultFilter.PrepareFoodItems(
                    query,
                    await _nutritionRepository.SearchFoodItemsAsync(query));

                return local
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
        }

        return FoodSearchResultFilter.PrepareFoodItems(query, foods);
    }

    public async Task<FoodItem> CreateCustomFoodAsync(CustomFoodInput input)
    {
        var name = input.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a food name.");

        if (input.Calories <= 0f)
            throw new ArgumentException("Enter calories greater than zero.");

        if (input.Protein < 0f || input.Carbs < 0f || input.Fats < 0f)
            throw new ArgumentException("Macros cannot be negative.");

        if (!FoodServingOptions.TryConvertToGrams(input.ServingAmount, input.ServingUnit, out var servingGrams))
            throw new ArgumentException("Use a serving unit that can be converted to grams.");

        var now = DateTime.UtcNow;
        var per100g = 100f / servingGrams;
        var food = new FoodItem
        {
            Name = name,
            Calories = input.Calories * per100g,
            Protein = input.Protein * per100g,
            Carbs = input.Carbs * per100g,
            Fats = input.Fats * per100g,
            ServingSize = input.ServingAmount,
            ServingSizeUnit = input.ServingUnit,
            IsCustom = true,
            DataType = "Custom",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _nutritionRepository.AddAsync(food);
        return food;
    }

    public Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date) =>
        _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);

    public Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId) =>
        _nutritionRepository.GetLoggedMealAsync(userId, loggedMealId);

    public async Task<List<MealTemplate>> GetReadyMealTemplatesAsync(
        int userId,
        CyclePhase phase,
        DateTime date)
    {
        if (_mealTemplateRepository is null)
            return [];

        var profile = await _userRepository.GetProfileAsync(userId);
        var plan = profile is null
            ? _nutritionPlanner.CreateFallbackPlan(phase)
            : _nutritionPlanner.CreateDailyPlan(profile, phase, date);
        var systemTemplates = await _mealTemplateRepository.GetSystemTemplatesAsync();
        var userTemplates = await _mealTemplateRepository.GetUserTemplatesAsync(userId);

        return MealTemplateRecommender.Recommend(
            systemTemplates.Concat(userTemplates),
            profile,
            plan,
            phase);
    }

    public async Task LogFoodAsync(int userId, int foodItemId, float grams, MealType mealType, DateTime loggedAt)
    {
        await LogMealAsync(
            userId,
            [new MealIngredientInput(foodItemId, grams)],
            mealType,
            loggedAt);
    }

    public async Task LogMealAsync(
        int userId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt)
    {
        var normalizedIngredients = await ValidateMealIngredientsAsync(userId, ingredients);

        await _nutritionRepository.AddLoggedMealAsync(BuildLoggedMeal(
            userId,
            loggedMealId: 0,
            normalizedIngredients,
            mealType,
            loggedAt));
    }

    public async Task UpdateMealAsync(
        int userId,
        int loggedMealId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt)
    {
        if (loggedMealId <= 0)
            throw new InvalidOperationException("Choose a meal to edit.");

        var normalizedIngredients = await ValidateMealIngredientsAsync(userId, ingredients);

        await _nutritionRepository.UpdateLoggedMealAsync(BuildLoggedMeal(
            userId,
            loggedMealId,
            normalizedIngredients,
            mealType,
            loggedAt));
    }

    private async Task<List<MealIngredientInput>> ValidateMealIngredientsAsync(
        int userId,
        IReadOnlyCollection<MealIngredientInput> ingredients)
    {
        if (ingredients.Count == 0)
            throw new InvalidOperationException("Add at least one ingredient to the meal.");

        var normalizedIngredients = ingredients
            .GroupBy(i => i.FoodItemId)
            .Select(group => new MealIngredientInput(group.Key, group.Sum(i => i.Grams)))
            .ToList();

        if (normalizedIngredients.Any(i => i.FoodItemId <= 0))
            throw new InvalidOperationException("Choose ingredients from the search results.");

        if (normalizedIngredients.Any(i => i.Grams <= 0f))
            throw new ArgumentOutOfRangeException(nameof(ingredients), "Every meal ingredient must have grams greater than zero.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            throw new InvalidOperationException("Current user no longer exists. Please sign in again.");

        var foods = await _nutritionRepository.GetFoodItemsByIdsAsync(
            normalizedIngredients.Select(ingredient => ingredient.FoodItemId));
        var foodsById = foods.ToDictionary(food => food.Id);

        if (normalizedIngredients.Any(ingredient => !foodsById.ContainsKey(ingredient.FoodItemId)))
            throw new InvalidOperationException("One or more ingredients are no longer available.");

        if (foods.Any(food => !HasCalories(food)))
            throw new InvalidOperationException("One or more ingredients are missing calories. Choose another product.");

        return normalizedIngredients;
    }

    private static LoggedMeal BuildLoggedMeal(
        int userId,
        int loggedMealId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt) =>
        new()
        {
            Id = loggedMealId,
            UserId = userId,
            Date = loggedAt.Date,
            LoggedAt = loggedAt,
            MealType = mealType,
            CreatedAt = DateTime.UtcNow,
            Entries = ingredients
                .Select(ingredient => new LoggedMealEntry
                {
                    FoodItemId = ingredient.FoodItemId,
                    Grams = ingredient.Grams
                })
                .ToList()
        };

    private static bool HasCalories(FoodItem food) =>
        food.Calories > 0f;
}
