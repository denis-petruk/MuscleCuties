using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public sealed class SuggestedMealService : ISuggestedMealService
{
    private const int MaxSuggestions = 4;
    private const float MinimumRemainingCalories = 50f;
    private const int VarietyWindowDays = 7;

    private readonly INutritionPlanner _nutritionPlanner;
    private readonly INutritionRepository _nutritionRepository;
    private readonly IUserRepository _userRepository;

    public SuggestedMealService(
        INutritionPlanner nutritionPlanner,
        INutritionRepository nutritionRepository,
        IUserRepository userRepository)
    {
        _nutritionPlanner = nutritionPlanner;
        _nutritionRepository = nutritionRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<SuggestedMeal>> SuggestAsync(
        int userId,
        MealType mealType,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        float consumedCalories)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        var plan = profile is not null
            ? _nutritionPlanner.CreateDailyPlan(profile, phase, date, breakfastPreference)
            : _nutritionPlanner.CreateFallbackPlan(phase, breakfastPreference);

        var mealTarget = plan.Meals.FirstOrDefault(m => m.MealType == mealType);
        if (mealTarget is null)
            return [];

        var remainingBudget = plan.Calories - consumedCalories;
        if (remainingBudget < MinimumRemainingCalories)
            return [];

        var clampedTarget = mealTarget with
        {
            Calories = MathF.Min(mealTarget.Calories, remainingBudget),
            Protein = MathF.Min(mealTarget.Protein, remainingBudget / 4f),
            Carbs = MathF.Min(mealTarget.Carbs, remainingBudget / 4f),
            Fats = MathF.Min(mealTarget.Fats, remainingBudget / 9f)
        };

        var dietaryTags = ParseDietaryTags(profile?.DietaryTags);
        var goal = profile?.Goal ?? UserGoal.MaintainHealth;
        var allFoods = await _nutritionRepository.GetAllAsync();
        var compatibleFoods = PreFilterByDietary(allFoods, dietaryTags);
        var daysSinceUsed = await BuildUsageMapAsync(userId, date);

        var conceptSource = mealType is MealType.Breakfast && breakfastPreference is BreakfastPreference.Sweet
            ? SweetBreakfastCatalog.GetConcepts()
            : SavouryMealCatalog.GetConcepts(mealType);

        var concepts = Shuffle(conceptSource);
        var results = new List<SuggestedMeal>();

        foreach (var concept in concepts)
        {
            if (concept.IncompatibleDietaryTags.Overlaps(dietaryTags))
                continue;

            var slotFoods = BuildSlotFoods(concept.Slots, compatibleFoods);
            if (slotFoods is null)
                continue;

            var meals = BuildConceptSuggestions(
                concept, slotFoods, clampedTarget, phase, dietaryTags, daysSinceUsed, goal);

            results.AddRange(meals);
        }

        return results
            .OrderByDescending(s => s.Score)
            .Take(MaxSuggestions)
            .ToList();
    }

    private static IReadOnlyList<FoodItem> PreFilterByDietary(
        IEnumerable<FoodItem> foods,
        IReadOnlySet<DietaryTag> dietaryTags)
    {
        return foods
            .Where(f => f.Calories > 0f && Ingredient.MatchesDietaryTags(f, dietaryTags))
            .ToList();
    }

    private static SlotFoodMap? BuildSlotFoods(
        IReadOnlyList<MealConceptSlot> slots,
        IReadOnlyList<FoodItem> compatibleFoods)
    {
        List<ResolvedIngredientEntry>? carbEntries = null;
        List<ResolvedIngredientEntry>? proteinEntries = null;
        List<ResolvedIngredientEntry>? vitaminEntries = null;
        List<ResolvedIngredientEntry>? sauceEntries = null;

        foreach (var slot in slots)
        {
            var resolved = ResolveSlotEntries(compatibleFoods, slot.IngredientEntries, slot.SlotType);

            if (resolved.Count == 0 && slot.Required)
                return null;

            if (resolved.Count == 0)
                continue;

            switch (slot.SlotType)
            {
                case MealConceptSlotType.CarbBase:
                    carbEntries ??= resolved;
                    break;
                case MealConceptSlotType.ProteinBase:
                    proteinEntries ??= resolved;
                    break;
                case MealConceptSlotType.VitaminBase:
                    vitaminEntries ??= resolved;
                    break;
                case MealConceptSlotType.Sauce:
                    sauceEntries ??= resolved;
                    break;
            }
        }

        if (proteinEntries is null || carbEntries is null || vitaminEntries is null)
            return null;

        return new SlotFoodMap(proteinEntries, carbEntries, vitaminEntries, sauceEntries);
    }

    private static List<ResolvedIngredientEntry> ResolveSlotEntries(
        IReadOnlyList<FoodItem> compatibleFoods,
        IReadOnlyList<SlotIngredientEntry> entries,
        MealConceptSlotType slotType)
    {
        var resolved = new List<ResolvedIngredientEntry>();
        var usedIds = new HashSet<int>();

        foreach (var entry in entries)
        {
            var match = FindBestMatch(compatibleFoods, entry.PreferredFoodTerms, usedIds);

            if (match is null && entry.Required)
                match = FindByClassifier(compatibleFoods, slotType, usedIds);

            if (match is not null)
            {
                usedIds.Add(match.Id);
                resolved.Add(new ResolvedIngredientEntry(match, entry.PortionShare));
            }
            else if (entry.Required)
            {
                return [];
            }
        }

        return resolved;
    }

    private static FoodItem? FindBestMatch(
        IReadOnlyList<FoodItem> compatibleFoods,
        IReadOnlyList<string> preferredTerms,
        IReadOnlySet<int> excludeIds)
    {
        foreach (var term in preferredTerms)
        {
            var candidates = compatibleFoods
                .Where(f => !excludeIds.Contains(f.Id) &&
                            f.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count > 0)
                return candidates[Random.Shared.Next(candidates.Count)];
        }

        return null;
    }

    private static FoodItem? FindByClassifier(
        IReadOnlyList<FoodItem> compatibleFoods,
        MealConceptSlotType slotType,
        IReadOnlySet<int> excludeIds)
    {
        var targetType = SlotToComponentType(slotType);

        return compatibleFoods.FirstOrDefault(f =>
            !excludeIds.Contains(f.Id) &&
            FoodComponentClassifier.Classify(f) == targetType);
    }

    private static ComponentType SlotToComponentType(MealConceptSlotType slotType)
    {
        return slotType switch
        {
            MealConceptSlotType.CarbBase => ComponentType.Carb,
            MealConceptSlotType.ProteinBase => ComponentType.Protein,
            MealConceptSlotType.VitaminBase => ComponentType.Vegetable,
            MealConceptSlotType.Sauce => ComponentType.Sauce,
            _ => ComponentType.Mixed
        };
    }

    private static List<SuggestedMeal> BuildConceptSuggestions(
        MealConcept concept,
        SlotFoodMap slotFoods,
        MealNutritionTarget target,
        CyclePhase phase,
        IReadOnlySet<DietaryTag> dietaryTags,
        IReadOnlyDictionary<int, int> daysSinceUsed,
        UserGoal goal)
    {
        var results = new List<SuggestedMeal>();

        var primaryProtein = slotFoods.Protein[0].Food;
        var primaryCarb = slotFoods.Carb[0].Food;
        var primaryVitamin = slotFoods.Vitamin[0].Food;
        var primarySauce = slotFoods.Sauce?.Count > 0 ? slotFoods.Sauce[0].Food : null;

        var style = MealComponentScorer.DetermineStyle(
            primaryProtein, primaryCarb, primaryVitamin, primarySauce);

        var score = MealComponentScorer.ScoreCombo(
            primaryProtein, primaryCarb, primaryVitamin, primarySauce,
            target, phase, dietaryTags, daysSinceUsed, goal, style);

        if (score < 0f)
            return results;

        var solution = SolveMultiIngredient(
            target, slotFoods, goal, style);

        if (solution is null)
            solution = SolveMultiIngredient(target, slotFoods, goal, style, relaxed: true);

        if (solution is null)
            return results;

        results.Add(new SuggestedMeal(
            concept.Name,
            concept.Description,
            solution.CarbComponent,
            solution.ProteinComponent,
            solution.VitaminComponent,
            solution.SauceComponent,
            solution.Total,
            score,
            style,
            concept.SpiceBlends));

        return results;
    }

    private static PortionSolution? SolveMultiIngredient(
        MealNutritionTarget target,
        SlotFoodMap slotFoods,
        UserGoal goal,
        MealStyle style,
        bool relaxed = false)
    {
        var primaryProtein = slotFoods.Protein[0].Food;
        var primaryCarb = slotFoods.Carb[0].Food;
        var primaryVitamin = slotFoods.Vitamin[0].Food;
        var primarySauce = slotFoods.Sauce?.Count > 0 ? slotFoods.Sauce[0].Food : null;

        var baseSolution = PortionSolver.Solve(
            target, primaryProtein, primaryCarb, primaryVitamin, primarySauce,
            goal, false, style, relaxed);

        if (baseSolution is null)
            return null;

        var proteinComponent = DistributeGrams(
            MealConceptSlotType.ProteinBase, slotFoods.Protein, baseSolution.ProteinComponent.TotalGrams);
        var carbComponent = DistributeGrams(
            MealConceptSlotType.CarbBase, slotFoods.Carb, baseSolution.CarbComponent.TotalGrams);
        var vitaminComponent = DistributeGrams(
            MealConceptSlotType.VitaminBase, slotFoods.Vitamin, baseSolution.VitaminComponent.TotalGrams);

        MealComponent? sauceComponent = null;
        if (slotFoods.Sauce is { Count: > 0 } && baseSolution.SauceComponent is not null)
        {
            sauceComponent = DistributeGrams(
                MealConceptSlotType.Sauce, slotFoods.Sauce, baseSolution.SauceComponent.TotalGrams);
        }

        var allMacros = new List<MacroNutrients>
        {
            proteinComponent.Macros,
            carbComponent.Macros,
            vitaminComponent.Macros
        };
        if (sauceComponent is not null)
            allMacros.Add(sauceComponent.Macros);

        var total = MacroNutrients.Sum(allMacros);

        return new PortionSolution(carbComponent, proteinComponent, vitaminComponent, sauceComponent, total);
    }

    private static MealComponent DistributeGrams(
        MealConceptSlotType slotType,
        IReadOnlyList<ResolvedIngredientEntry> entries,
        float totalGrams)
    {
        if (entries.Count == 1)
        {
            var grams = PortionSolver.SnapToServing(entries[0].Food, totalGrams);
            var ingredient = new Ingredient(entries[0].Food, grams);
            return new MealComponent(slotType, [ingredient]);
        }

        var totalShare = entries.Sum(e => e.PortionShare);
        var ingredients = new List<Ingredient>(entries.Count);

        foreach (var entry in entries)
        {
            var share = totalShare > 0f ? entry.PortionShare / totalShare : 1f / entries.Count;
            var rawGrams = totalGrams * share;
            var grams = PortionSolver.SnapToServing(entry.Food, rawGrams);
            if (grams > 0f)
                ingredients.Add(new Ingredient(entry.Food, grams));
        }

        if (ingredients.Count == 0)
        {
            var grams = PortionSolver.SnapToServing(entries[0].Food, totalGrams);
            ingredients.Add(new Ingredient(entries[0].Food, grams));
        }

        return new MealComponent(slotType, ingredients);
    }

    private async Task<IReadOnlyDictionary<int, int>> BuildUsageMapAsync(int userId, DateTime date)
    {
        var startDate = date.AddDays(-VarietyWindowDays);
        var recentMeals = await _nutritionRepository.GetLoggedMealsByDateRangeAsync(userId, startDate, date);

        var usage = new Dictionary<int, int>();
        foreach (var meal in recentMeals)
            foreach (var entry in meal.Entries)
            {
                var daysAgo = (date - meal.Date).Days;
                if (!usage.TryGetValue(entry.FoodItemId, out var existing) || daysAgo < existing)
                    usage[entry.FoodItemId] = daysAgo;
            }

        return usage;
    }

    private static IReadOnlySet<DietaryTag> ParseDietaryTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<DietaryTag>();

        var tags = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<DietaryTag>(part, true, out var tag) ? tag : (DietaryTag?)null)
            .Where(tag => tag.HasValue && tag.Value != DietaryTag.None)
            .Select(tag => tag!.Value)
            .ToHashSet();

        if (tags.Contains(DietaryTag.Vegan))
            tags.Add(DietaryTag.Vegetarian);

        return tags;
    }

    private static List<T> Shuffle<T>(IReadOnlyList<T> source)
    {
        var list = new List<T>(source);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private sealed record ResolvedIngredientEntry(FoodItem Food, float PortionShare);

    private sealed record SlotFoodMap(
        IReadOnlyList<ResolvedIngredientEntry> Protein,
        IReadOnlyList<ResolvedIngredientEntry> Carb,
        IReadOnlyList<ResolvedIngredientEntry> Vitamin,
        IReadOnlyList<ResolvedIngredientEntry>? Sauce);
}
