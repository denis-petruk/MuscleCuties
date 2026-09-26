using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Inputs;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition;

public class NutritionService : INutritionService
{
    private readonly IFoodSyncService? _foodSyncService;
    private readonly IHealthSyncService? _healthSyncService;
    private readonly ISuggestedMealService? _suggestedMealService;
    private readonly INutritionPlanner _nutritionPlanner;
    private readonly INutritionRepository _nutritionRepository;
    private readonly IUserRepository _userRepository;

    public NutritionService(
        IUserRepository userRepository,
        INutritionRepository nutritionRepository,
        ICalorieCalculator calorieCalculator,
        IFoodSyncService? foodSyncService = null,
        INutritionPlanner? nutritionPlanner = null,
        IHealthSyncService? healthSyncService = null,
        ISuggestedMealService? suggestedMealService = null)
    {
        _userRepository = userRepository;
        _nutritionRepository = nutritionRepository;
        _nutritionPlanner = nutritionPlanner ?? new NutritionPlanner(calorieCalculator);
        _foodSyncService = foodSyncService;
        _healthSyncService = healthSyncService;
        _suggestedMealService = suggestedMealService;
    }

    public async Task<NutritionPlan> GetDailyPlanAsync(
        int userId,
        CyclePhase phase,
        DateTime date,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        return await CreateDailyPlanAsync(userId, profile, phase, date, breakfastPreference);
    }

    private async Task<NutritionPlan> CreateDailyPlanAsync(
        int userId,
        UserProfile? profile,
        CyclePhase phase,
        DateTime date,
        BreakfastPreference breakfastPreference)
    {
        if (profile is null)
            return _nutritionPlanner.CreateFallbackPlan(phase, breakfastPreference);

        var plan = _nutritionPlanner.CreateDailyPlan(profile, phase, date, breakfastPreference);
        return await ApplyHealthEnergyAdjustmentAsync(userId, profile, phase, date, plan);
    }

    public async Task<(float Calories, float Protein, float Carbs, float Fats)> CalculateDailyTargetsAsync(int userId,
        CyclePhase phase)
    {
        var plan = await GetDailyPlanAsync(userId, phase, DateTime.UtcNow);
        return (plan.Calories, plan.Protein, plan.Carbs, plan.Fats);
    }

    public async Task<MacroNutrients> GetConsumedTotalsAsync(int userId, DateTime date)
    {
        var meals = await _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
        return MacroNutrients.SumMealEntries(meals.SelectMany(m => m.Entries));
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

    public Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date)
    {
        return _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
    }

    public Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId)
    {
        return _nutritionRepository.GetLoggedMealAsync(userId, loggedMealId);
    }

    public async Task<IReadOnlyList<SuggestedMeal>> GetSuggestedMealsAsync(
        int userId,
        MealType mealType,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        IReadOnlySet<string>? excludeConceptNames = null)
    {
        if (_suggestedMealService is null)
            return [];

        var consumed = await GetConsumedTotalsAsync(userId, date);
        return await _suggestedMealService.SuggestAsync(
            userId, mealType, breakfastPreference, phase, date, consumed.Calories, excludeConceptNames);
    }

    public async Task<DailyMealSuggestionPlan> GetSuggestedMealPlanAsync(
        int userId,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        var plan = await CreateDailyPlanAsync(userId, profile, phase, date, breakfastPreference);
        var loggedMeals = await _nutritionRepository.GetLoggedMealsByDateAsync(userId, date);
        var consumed = MacroNutrients.SumMealEntries(loggedMeals.SelectMany(meal => meal.Entries));
        var dailyTargetCalories = SafeNonNegative(plan.Calories);
        var remainingCalories = SafeNonNegative(dailyTargetCalories - SafeNonNegative(consumed.Calories));
        var loggedTypes = loggedMeals.Select(meal => meal.MealType).ToHashSet();
        var remainingTargets = plan.Meals
            .Where(target => !loggedTypes.Contains(target.MealType))
            .Sum(target => SafeNonNegative(target.Calories));
        var targetScale = remainingTargets > 0f
            ? Math.Clamp(remainingCalories / remainingTargets, 0f, 1f)
            : 0f;
        if (!float.IsFinite(targetScale))
            targetScale = 0f;
        var adjustedTargets = plan.Meals
            .Where(target => !loggedTypes.Contains(target.MealType))
            .Select(target => ScaleMealTarget(target, targetScale))
            .Where(target => target.Calories >= 50f && remainingCalories >= 50f)
            .ToList();
        var suggestionsByType = _suggestedMealService is null || adjustedTargets.Count == 0
            ? new Dictionary<MealType, IReadOnlyList<SuggestedMeal>>()
            : await _suggestedMealService.SuggestPlanAsync(
                userId,
                adjustedTargets,
                breakfastPreference,
                phase,
                date,
                profile);
        var meals = new List<DailyMealSuggestion>(plan.Meals.Count);

        foreach (var target in plan.Meals)
        {
            var isAlreadyLogged = loggedTypes.Contains(target.MealType);
            var adjustedTarget = ScaleMealTarget(target, isAlreadyLogged ? 1f : targetScale);
            var suggestion = !isAlreadyLogged && suggestionsByType.TryGetValue(target.MealType, out var suggestions)
                ? suggestions.FirstOrDefault()
                : null;

            meals.Add(new DailyMealSuggestion(
                target.MealType,
                adjustedTarget,
                isAlreadyLogged,
                suggestion));
        }

        return new DailyMealSuggestionPlan(dailyTargetCalories, remainingCalories, meals);
    }

    public async Task<IReadOnlyList<SuggestedMeal>> GetSuggestedMealsForTargetAsync(
        int userId,
        MealNutritionTarget target,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        IReadOnlySet<string>? excludeConceptNames = null)
    {
        if (_suggestedMealService is null || target.Calories < 50f)
            return [];

        return await _suggestedMealService.SuggestAsync(
            userId,
            target.MealType,
            breakfastPreference,
            phase,
            date,
            consumedCalories: 0f,
            excludeConceptNames: excludeConceptNames,
            targetOverride: target);
    }

    private static MealNutritionTarget ScaleMealTarget(MealNutritionTarget target, float scale)
    {
        scale = float.IsFinite(scale) ? Math.Clamp(scale, 0f, 1f) : 0f;
        return target with
        {
            Calories = SafeNonNegative(target.Calories * scale),
            Protein = SafeNonNegative(target.Protein * scale),
            Carbs = SafeNonNegative(target.Carbs * scale),
            Fats = SafeNonNegative(target.Fats * scale)
        };
    }

    private static float SafeNonNegative(float value)
        => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;

    public async Task LogMealAsync(
        int userId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt)
    {
        var normalizedIngredients = await ValidateMealIngredientsAsync(userId, ingredients);

        await _nutritionRepository.AddLoggedMealAsync(BuildLoggedMeal(
            userId,
            0,
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

    private async Task<NutritionPlan> ApplyHealthEnergyAdjustmentAsync(
        int userId,
        UserProfile profile,
        CyclePhase phase,
        DateTime date,
        NutritionPlan plan)
    {
        if (_healthSyncService is null)
            return plan;

        var summary = await _healthSyncService.GetCachedWeeklySummaryAsync(userId);
        var age = CalculateAge(profile.DateOfBirth, date);
        var calorieAdjustment = HealthEnergyAdjustment.CalculateDailyCalories(
            summary,
            profile.Weight,
            age,
            profile.WorkoutDaysPerWeek,
            phase);

        if (Math.Abs(calorieAdjustment) < 1f)
            return plan;

        var calories = RoundToNearest(Math.Clamp(plan.Calories + calorieAdjustment, 1200f, 4000f), 10f);
        var macros = RebalanceMacros(calories, plan.Protein, plan.Carbs, plan.Fats, profile.Weight);

        return plan with
        {
            Calories = calories,
            Protein = macros.Protein,
            Carbs = macros.Carbs,
            Fats = macros.Fats,
            Meals = BuildMealTargets(calories, macros.Protein, macros.Carbs, macros.Fats, plan.BreakfastPreference)
        };
    }

    private static (float Protein, float Carbs, float Fats) RebalanceMacros(
        float calories,
        float protein,
        float carbs,
        float fats,
        float weightKg)
    {
        var proteinCalories = protein * 4f;
        var carbCalories = MathF.Max(carbs * 4f, 0f);
        var fatCalories = MathF.Max(fats * 9f, 0f);
        var nonProteinCalories = MathF.Max(calories - proteinCalories, calories * 0.35f);
        var fatShare = carbCalories + fatCalories > 0f
            ? fatCalories / (carbCalories + fatCalories)
            : 0.32f;
        var minimumFats = MathF.Max(weightKg * 0.6f, 35f);
        var adjustedFats = MathF.Max(minimumFats, nonProteinCalories * fatShare / 9f);
        var adjustedCarbs = MathF.Max((calories - proteinCalories - adjustedFats * 9f) / 4f, 0f);

        return (
            RoundToNearest(protein, 1f),
            RoundToNearest(adjustedCarbs, 1f),
            RoundToNearest(adjustedFats, 1f));
    }

    private static IReadOnlyCollection<MealNutritionTarget> BuildMealTargets(
        float calories,
        float protein,
        float carbs,
        float fats,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        const float minMainShare = 0.75f;
        const float maxMainShare = 0.85f;

        var (breakfastShare, lunchShare, dinnerShare) = breakfastPreference switch
        {
            BreakfastPreference.Sweet => (0.20f, 0.32f, 0.28f),
            _ => (0.25f, 0.33f, 0.25f)
        };

        var rawMainShare = breakfastShare + lunchShare + dinnerShare;
        var clampedMainShare = Math.Clamp(rawMainShare, minMainShare, maxMainShare);

        if (MathF.Abs(rawMainShare - clampedMainShare) > 0.001f && rawMainShare > 0f)
        {
            var scale = clampedMainShare / rawMainShare;
            breakfastShare *= scale;
            lunchShare *= scale;
            dinnerShare *= scale;
        }

        var snackShare = 1f - breakfastShare - lunchShare - dinnerShare;

        return
        [
            BuildMealTarget(MealType.Breakfast, breakfastShare, calories, protein, carbs, fats),
            BuildMealTarget(MealType.Lunch, lunchShare, calories, protein, carbs, fats),
            BuildMealTarget(MealType.Dinner, dinnerShare, calories, protein, carbs, fats),
            BuildMealTarget(MealType.Snack, snackShare, calories, protein, carbs, fats)
        ];
    }

    private static MealNutritionTarget BuildMealTarget(
        MealType mealType,
        float share,
        float calories,
        float protein,
        float carbs,
        float fats)
    {
        return new MealNutritionTarget(
            mealType,
            RoundToNearest(calories * share, 10f),
            RoundToNearest(protein * share, 1f),
            RoundToNearest(carbs * share, 1f),
            RoundToNearest(fats * share, 1f));
    }

    private static int CalculateAge(DateTime dateOfBirth, DateTime date)
    {
        var age = date.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > date.Date.AddYears(-age))
            age--;

        return Math.Clamp(age, 12, 100);
    }

    private static float RoundToNearest(float value, float nearest)
    {
        return MathF.Round(value / nearest) * nearest;
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
            throw new ArgumentOutOfRangeException(nameof(ingredients),
                "Every meal ingredient must have grams greater than zero.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            throw new InvalidOperationException("Current user no longer exists. Please sign in again.");

        var foods = await _nutritionRepository.GetFoodItemsByIdsAsync(
            normalizedIngredients.Select(ingredient => ingredient.FoodItemId));
        var foodsById = foods.ToDictionary(food => food.Id);

        if (normalizedIngredients.Any(ingredient => !foodsById.ContainsKey(ingredient.FoodItemId)))
            throw new InvalidOperationException("One or more ingredients are no longer available.");

        if (foods.Any(food => !HasCalories(food)))
            throw new InvalidOperationException(
                "One or more ingredients are missing calories. Choose another product.");

        return normalizedIngredients;
    }

    private static LoggedMeal BuildLoggedMeal(
        int userId,
        int loggedMealId,
        IReadOnlyCollection<MealIngredientInput> ingredients,
        MealType mealType,
        DateTime loggedAt)
    {
        return new LoggedMeal
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
    }

    private static bool HasCalories(FoodItem food)
    {
        return food.Calories > 0f;
    }
}
