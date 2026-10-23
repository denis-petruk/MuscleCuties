using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
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
    private const int ApiSearchPageSize = 15;
    private const float MinimumRemainingCalories = 50f;
    private const int VarietyWindowDays = 7;

    private const float DefaultCondimentGrams = 10f;
    private static readonly TimeSpan ApiSuggestionTimeout = TimeSpan.FromSeconds(4);
    private readonly IFallbackMealProvider _fallbackMealProvider;
    private readonly IFoodSyncService _foodSyncService;

    private readonly INutritionPlanner _nutritionPlanner;
    private readonly INutritionRepository _nutritionRepository;
    private readonly IUserRepository _userRepository;

    public SuggestedMealService(
        INutritionPlanner nutritionPlanner,
        INutritionRepository nutritionRepository,
        IFoodSyncService foodSyncService,
        IUserRepository userRepository,
        IFallbackMealProvider fallbackMealProvider)
    {
        _nutritionPlanner = nutritionPlanner;
        _nutritionRepository = nutritionRepository;
        _foodSyncService = foodSyncService;
        _userRepository = userRepository;
        _fallbackMealProvider = fallbackMealProvider;
    }

    public async Task<IReadOnlyList<SuggestedMeal>> SuggestAsync(
        int userId,
        MealType mealType,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        float consumedCalories,
        IReadOnlySet<string>? excludeConceptNames = null,
        MealNutritionTarget? targetOverride = null)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        var plan = profile is not null
            ? _nutritionPlanner.CreateDailyPlan(profile, phase, date, breakfastPreference)
            : _nutritionPlanner.CreateFallbackPlan(phase, breakfastPreference);

        var mealTarget = targetOverride ?? plan.Meals.FirstOrDefault(m => m.MealType == mealType);
        if (mealTarget is null)
            return [];

        var safeConsumedCalories = float.IsFinite(consumedCalories)
            ? MathF.Max(0f, consumedCalories)
            : 0f;
        var remainingBudget = plan.Calories - safeConsumedCalories;
        if (remainingBudget < MinimumRemainingCalories)
            return [];

        var clampedTarget = mealTarget with
        {
            Calories = MathF.Min(mealTarget.Calories, remainingBudget),
            Protein = MathF.Min(mealTarget.Protein, remainingBudget / 4f),
            Carbs = MathF.Min(mealTarget.Carbs, remainingBudget / 4f),
            Fats = MathF.Min(mealTarget.Fats, remainingBudget / 9f)
        };

        var conceptSource = mealType is MealType.Breakfast && breakfastPreference is BreakfastPreference.Sweet
            ? SweetBreakfastCatalog.GetConcepts()
            : SavouryMealCatalog.GetConcepts(mealType);
        var dietaryTags = ParseDietaryTags(profile?.DietaryTags);
        var goal = profile?.Goal ?? UserGoal.MaintainHealth;
        var concepts = Shuffle(conceptSource)
            .Where(concept => excludeConceptNames is null || !excludeConceptNames.Contains(concept.Name))
            .Where(concept => !concept.IncompatibleDietaryTags.Overlaps(dietaryTags))
            .ToList();
        var daysSinceUsed = await BuildUsageMapAsync(userId, date);
        var results = new List<SuggestedMeal>();

        // Common suggestions should be usable without a network round trip.
        var localFoods = PreFilterByDietary(await _nutritionRepository.GetAllAsync(), dietaryTags);
        AddSuggestions(results, concepts, localFoods, clampedTarget, phase, dietaryTags, daysSinceUsed, goal);

        IReadOnlyList<FoodItem> apiFoods = [];
        if (results.Count < MaxSuggestions)
        {
            apiFoods = PreFilterByDietary(await LoadApiFoodsAsync(concepts, dietaryTags), dietaryTags);
            var combinedFoods = localFoods
                .Concat(apiFoods)
                .DistinctBy(GetFoodIdentityKey)
                .ToList();
            AddSuggestions(results, concepts, combinedFoods, clampedTarget, phase, dietaryTags, daysSinceUsed, goal);
        }

        if (results.Count < MaxSuggestions)
        {
            var fallbackFoods = PreFilterByDietary(
                await LoadFallbackFoodsAsync(mealType, breakfastPreference, dietaryTags),
                dietaryTags);
            var combinedFoods = localFoods
                .Concat(apiFoods)
                .Concat(fallbackFoods)
                .DistinctBy(GetFoodIdentityKey)
                .ToList();
            AddSuggestions(results, concepts, combinedFoods, clampedTarget, phase, dietaryTags, daysSinceUsed, goal);
        }

        return results
            .OrderByDescending(s => s.Score)
            .Take(MaxSuggestions)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<MealType, IReadOnlyList<SuggestedMeal>>> SuggestPlanAsync(
        int userId,
        IReadOnlyList<MealNutritionTarget> targets,
        BreakfastPreference breakfastPreference,
        CyclePhase phase,
        DateTime date,
        UserProfile? profile,
        IReadOnlySet<string>? excludeConceptNames = null)
    {
        if (targets.Count == 0)
            return new Dictionary<MealType, IReadOnlyList<SuggestedMeal>>();

        var dietaryTags = ParseDietaryTags(profile?.DietaryTags);
        var goal = profile?.Goal ?? UserGoal.MaintainHealth;
        var conceptsByType = targets.ToDictionary(
            target => target.MealType,
            target => Shuffle(GetConcepts(target.MealType, breakfastPreference))
                .Where(concept => excludeConceptNames is null || !excludeConceptNames.Contains(concept.Name))
                .Where(concept => !concept.IncompatibleDietaryTags.Overlaps(dietaryTags))
                .ToList());
        var allConcepts = conceptsByType.Values.SelectMany(concepts => concepts).ToList();
        var daysSinceUsed = await BuildUsageMapAsync(userId, date);
        var results = targets.ToDictionary(target => target.MealType, _ => new List<SuggestedMeal>());

        var localFoods = PreFilterByDietary(await _nutritionRepository.GetAllAsync(), dietaryTags);
        AddPlanSuggestions(results, targets, conceptsByType, localFoods, phase, dietaryTags, daysSinceUsed, goal);

        if (results.Values.Any(meals => meals.Count < MaxSuggestions))
        {
            var apiFoods = PreFilterByDietary(await LoadApiFoodsAsync(allConcepts, dietaryTags), dietaryTags);
            var combinedFoods = localFoods.Concat(apiFoods).DistinctBy(GetFoodIdentityKey).ToList();
            AddPlanSuggestions(results, targets, conceptsByType, combinedFoods, phase, dietaryTags, daysSinceUsed,
                goal);
        }

        return results.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SuggestedMeal>)pair.Value.OrderByDescending(meal => meal.Score).Take(MaxSuggestions)
                .ToList());
    }

    private async Task<IReadOnlyList<FoodItem>> LoadApiFoodsAsync(
        IReadOnlyList<MealConcept> concepts,
        IReadOnlySet<DietaryTag> dietaryTags)
    {
        var queries = BuildApiSearchQueries(concepts, dietaryTags);
        return await SearchAndDeduplicateAsync(queries);
    }

    private async Task<IReadOnlyList<FoodItem>> LoadFallbackFoodsAsync(
        MealType mealType,
        BreakfastPreference breakfastPreference,
        IReadOnlySet<DietaryTag> dietaryTags)
    {
        var queries = _fallbackMealProvider
            .GetMealsForType(mealType, breakfastPreference, dietaryTags)
            .SelectMany(meal => meal.IngredientNames)
            .Where(term => IsSearchTermCompatible(term, dietaryTags))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(IsProteinAnchorSearchTerm)
            .ThenBy(IsCarbSearchTerm)
            .Take(6)
            .ToList();

        if (queries.Count == 0)
            return [];

        return await SearchAndDeduplicateAsync(queries);
    }

    private static void AddPlanSuggestions(
        IDictionary<MealType, List<SuggestedMeal>> results,
        IReadOnlyList<MealNutritionTarget> targets,
        IReadOnlyDictionary<MealType, List<MealConcept>> conceptsByType,
        IReadOnlyList<FoodItem> foods,
        CyclePhase phase,
        IReadOnlySet<DietaryTag> dietaryTags,
        IReadOnlyDictionary<int, int> daysSinceUsed,
        UserGoal goal)
    {
        foreach (var target in targets)
        {
            var meals = results[target.MealType];
            if (meals.Count >= MaxSuggestions)
                continue;

            AddSuggestions(
                meals,
                conceptsByType[target.MealType],
                foods,
                target,
                phase,
                dietaryTags,
                daysSinceUsed,
                goal);
        }
    }

    private static IReadOnlyList<MealConcept> GetConcepts(
        MealType mealType,
        BreakfastPreference breakfastPreference)
    {
        return mealType is MealType.Breakfast && breakfastPreference is BreakfastPreference.Sweet
            ? SweetBreakfastCatalog.GetConcepts()
            : SavouryMealCatalog.GetConcepts(mealType);
    }

    private static IReadOnlyList<string> BuildApiSearchQueries(
        IReadOnlyList<MealConcept> concepts,
        IReadOnlySet<DietaryTag> dietaryTags)
    {
        var queries = new List<string>(8);
        var slotOrder = new[]
        {
            MealConceptSlotType.ProteinBase,
            MealConceptSlotType.CarbBase,
            MealConceptSlotType.VitaminBase,
            MealConceptSlotType.Sauce
        };

        foreach (var slotType in slotOrder)
        {
            var terms = concepts
                .SelectMany(concept => concept.Slots)
                .Where(slot => slot.SlotType == slotType)
                .SelectMany(slot => slot.IngredientEntries)
                .SelectMany(entry => entry.PreferredFoodTerms)
                .Where(term => IsSearchTermCompatible(term, dietaryTags))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2);

            foreach (var term in terms)
            {
                if (queries.Count >= 6)
                    break;

                if (!queries.Contains(term, StringComparer.OrdinalIgnoreCase))
                    queries.Add(term);
            }
        }

        if (HasRestrictiveDietarySetting(dietaryTags))
            AddDecoyIngredientQueries(queries, dietaryTags);

        return queries;
    }

    private static bool HasRestrictiveDietarySetting(IReadOnlySet<DietaryTag> dietaryTags)
    {
        return dietaryTags.Any(tag => tag is
            DietaryTag.Vegan or
            DietaryTag.Vegetarian or
            DietaryTag.GlutenFree or
            DietaryTag.LactoseFree);
    }

    private static void AddDecoyIngredientQueries(
        ICollection<string> queries,
        IReadOnlySet<DietaryTag> dietaryTags)
    {
        var decoys = new[]
        {
            "tofu",
            "tempeh",
            "edamame",
            "pea protein",
            "brown rice",
            "spinach",
            "sweet potato"
        };

        foreach (var decoy in decoys.Where(term => IsSearchTermCompatible(term, dietaryTags)))
        {
            if (queries.Count >= 6)
                break;

            if (!queries.Contains(decoy, StringComparer.OrdinalIgnoreCase))
                queries.Add(decoy);
        }
    }

    private static bool IsSearchTermCompatible(string term, IReadOnlySet<DietaryTag> dietaryTags)
    {
        if (dietaryTags.Contains(DietaryTag.Vegan) &&
            (ContainsAny(term, "beef", "chicken", "egg", "fish", "ham", "honey", "salmon", "tuna", "turkey") ||
             (term.Contains("yogurt", StringComparison.OrdinalIgnoreCase) && !IsPlantYogurtTerm(term))))
            return false;

        if (dietaryTags.Contains(DietaryTag.Vegetarian) && ContainsAny(
                term, "beef", "chicken", "fish", "ham", "salmon", "tuna", "turkey"))
            return false;

        if (dietaryTags.Contains(DietaryTag.GlutenFree) &&
            ContainsAny(term, "bread", "flour tortilla") &&
            !term.Contains("gluten-free", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPlantYogurtTerm(string term)
    {
        return term.Contains("yogurt", StringComparison.OrdinalIgnoreCase) &&
               ContainsAny(term, "soy", "soya", "coconut", "almond", "oat", "cashew", "plant-based", "plant based");
    }

    private static void AddSuggestions(
        ICollection<SuggestedMeal> results,
        IReadOnlyList<MealConcept> concepts,
        IReadOnlyList<FoodItem> compatibleFoods,
        MealNutritionTarget target,
        CyclePhase phase,
        IReadOnlySet<DietaryTag> dietaryTags,
        IReadOnlyDictionary<int, int> daysSinceUsed,
        UserGoal goal)
    {
        if (compatibleFoods.Count == 0)
            return;

        var existingConcepts = results.Select(result => result.ConceptName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var concept in concepts)
        {
            if (results.Count >= MaxSuggestions)
                break;

            if (existingConcepts.Contains(concept.Name))
                continue;

            var isVeganBreakfastBurger = dietaryTags.Contains(DietaryTag.Vegan) &&
                                         (concept.Name.Equals("Breakfast Burger", StringComparison.OrdinalIgnoreCase) ||
                                          concept.Name.Equals("Vegan Breakfast Burger",
                                              StringComparison.OrdinalIgnoreCase));
            var conceptFoods = isVeganBreakfastBurger
                ? compatibleFoods.Where(food =>
                    !food.Name.Contains("black bean", StringComparison.OrdinalIgnoreCase) &&
                    (!FoodComponentClassifier.IsProteinAnchor(food) ||
                     ContainsAny(food.Name, "TVP", "textured vegetable protein", "tofu"))).ToList()
                : compatibleFoods;
            var conceptSlots = isVeganBreakfastBurger
                ? concept.Slots.Select(slot => slot.SlotType == MealConceptSlotType.ProteinBase
                    ? slot with
                    {
                        IngredientEntries =
                        [
                            new SlotIngredientEntry(
                                ["TVP", "textured vegetable protein", "tofu"], 1f, true)
                        ]
                    }
                    : slot).ToList()
                : concept.Slots;
            var slotFoods = BuildSlotFoods(conceptSlots, conceptFoods);
            if (slotFoods is null)
                continue;

            foreach (var suggestion in BuildConceptSuggestions(
                         concept, slotFoods, conceptFoods, target, phase, dietaryTags, daysSinceUsed, goal))
            {
                results.Add(suggestion);
                existingConcepts.Add(suggestion.ConceptName);
            }
        }
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
        return MealConceptSlotResolver.BuildSlotFoods(slots, compatibleFoods);
    }

    private static string GetFoodIdentityKey(FoodItem food)
    {
        if (food.FdcId.HasValue)
            return $"fdc:{food.FdcId.Value}";

        if (food.Id > 0)
            return $"id:{food.Id}";

        return $"name:{food.Name}";
    }

    private static bool IsProteinAnchorSearchTerm(string term)
    {
        return ContainsAny(term, "tofu", "tempeh", "edamame", "protein", "seitan", "egg", "chicken", "turkey", "fish",
            "salmon", "tuna", "yogurt", "lentil", "chickpea", "bean");
    }

    private static bool IsCarbSearchTerm(string term)
    {
        return ContainsAny(term, "banana", "bread", "corn", "oat", "pasta", "quinoa", "rice", "sweet potato",
            "tortilla");
    }

    private static List<SuggestedMeal> BuildConceptSuggestions(
        MealConcept concept,
        SlotFoodMap slotFoods,
        IReadOnlyList<FoodItem> compatibleFoods,
        MealNutritionTarget target,
        CyclePhase phase,
        IReadOnlySet<DietaryTag> dietaryTags,
        IReadOnlyDictionary<int, int> daysSinceUsed,
        UserGoal goal)
    {
        var results = new List<SuggestedMeal>();

        var (primaryProtein, primaryCarb, primaryVitamin, primarySauce) = ExtractPrimaryFoods(slotFoods);

        var style = concept.PreferredStyle
                    ?? MealComponentScorer.DetermineStyle(
                        primaryProtein, primaryCarb, primaryVitamin, primarySauce);

        var score = MealComponentScorer.ScoreCombo(
            primaryProtein, primaryCarb, primaryVitamin, primarySauce,
            target, phase, dietaryTags, daysSinceUsed, goal, style);

        if (score < 0f)
            return results;

        var solution = SolveMultiIngredient(
            target, slotFoods, goal, style);

        if (solution is null)
            solution = SolveMultiIngredient(target, slotFoods, goal, style, true);

        if (solution is null)
            return results;

        solution = EnsureZeroCalorieCondiment(solution, compatibleFoods);

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

    private static PortionSolution EnsureZeroCalorieCondiment(
        PortionSolution solution,
        IReadOnlyList<FoodItem> compatibleFoods)
    {
        var existingIds = CollectUsedFoodIds(solution);

        if (solution.SauceComponent is not null &&
            solution.SauceComponent.Ingredients.Any(i =>
                FoodComponentClassifier.IsZeroCalorieCondiment(i.Food)))
            return solution;

        var condiment = FindZeroCalorieCondiment(compatibleFoods, existingIds);
        if (condiment is null)
            return solution;

        var condimentIngredient = new Ingredient(condiment, DefaultCondimentGrams);

        if (solution.SauceComponent is not null)
        {
            var ingredients = solution.SauceComponent.Ingredients
                .Append(condimentIngredient).ToList();
            var updatedSauce = new MealComponent(MealConceptSlotType.Sauce, ingredients);
            return solution with
            {
                SauceComponent = updatedSauce,
                Total = MacroNutrients.Sum([
                    solution.CarbComponent.Macros,
                    solution.ProteinComponent.Macros,
                    solution.VitaminComponent.Macros,
                    updatedSauce.Macros
                ])
            };
        }

        var newSauce = new MealComponent(MealConceptSlotType.Sauce, [condimentIngredient]);
        return solution with
        {
            SauceComponent = newSauce,
            Total = MacroNutrients.Sum([
                solution.CarbComponent.Macros,
                solution.ProteinComponent.Macros,
                solution.VitaminComponent.Macros,
                newSauce.Macros
            ])
        };
    }

    private static FoodItem? FindZeroCalorieCondiment(
        IReadOnlyList<FoodItem> foods,
        IReadOnlySet<int> excludeIds)
    {
        return foods.FirstOrDefault(food =>
            !excludeIds.Contains(food.Id) &&
            FoodComponentClassifier.IsZeroCalorieCondiment(food));
    }

    private static HashSet<int> CollectUsedFoodIds(PortionSolution solution)
    {
        var ids = new HashSet<int>();
        AddComponentFoodIds(ids, solution.CarbComponent);
        AddComponentFoodIds(ids, solution.ProteinComponent);
        AddComponentFoodIds(ids, solution.VitaminComponent);
        if (solution.SauceComponent is not null)
            AddComponentFoodIds(ids, solution.SauceComponent);
        return ids;
    }

    private static void AddComponentFoodIds(HashSet<int> ids, MealComponent component)
    {
        foreach (var ingredient in component.Ingredients)
            ids.Add(ingredient.Food.Id);
    }

    private static PortionSolution? SolveMultiIngredient(
        MealNutritionTarget target,
        SlotFoodMap slotFoods,
        UserGoal goal,
        MealStyle style,
        bool relaxed = false)
    {
        var (primaryProtein, primaryCarb, primaryVitamin, primarySauce) = ExtractPrimaryFoods(slotFoods);

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
            sauceComponent = DistributeGrams(
                MealConceptSlotType.Sauce, slotFoods.Sauce, baseSolution.SauceComponent.TotalGrams);

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

    private static (FoodItem Protein, FoodItem Carb, FoodItem Vitamin, FoodItem? Sauce) ExtractPrimaryFoods(
        SlotFoodMap slotFoods)
    {
        return (
            slotFoods.Protein[0].Food,
            slotFoods.Carb[0].Food,
            slotFoods.Vitamin[0].Food,
            slotFoods.Sauce?.Count > 0 ? slotFoods.Sauce[0].Food : null);
    }

    private async Task<IReadOnlyList<FoodItem>> SearchAndDeduplicateAsync(IReadOnlyList<string> queries)
    {
        var foods = new List<FoodItem>();
        using var timeout = new CancellationTokenSource(ApiSuggestionTimeout);
        foreach (var query in queries)
            try
            {
                foods.AddRange(await _foodSyncService.SearchAsync(
                    query,
                    cancellationToken: timeout.Token));
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException
                                                  or TaskCanceledException)
            {
                break;
            }

        return foods
            .Where(food => food.FdcId.HasValue)
            .DistinctBy(food => food.FdcId)
            .ToList();
    }
}
