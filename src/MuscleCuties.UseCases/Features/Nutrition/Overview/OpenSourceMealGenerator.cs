using System.Collections.Concurrent;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

/// <summary>
/// Thread-safe, bounded cache of open-source USDA generic ingredient profiles reused across requests
/// within a server process. Contains only public generic food nutrition data, never user records.
/// </summary>
public sealed class OpenSourceIngredientCache(TimeProvider? timeProvider = null)
{
    private const int MaxEntries = 256;
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromHours(12);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns a cached lookup result, so a repeated query does not hit the network again.</summary>
    public bool TryGet(string query, out ExternalFoodItem item)
    {
        var now = _timeProvider.GetUtcNow();
        if (_entries.TryGetValue(query, out var entry) && entry.ExpiresAtUtc > now)
        {
            item = entry.Item;
            return true;
        }

        item = default!;
        return false;
    }

    /// <summary>Stores a lookup result under its query for the cache's lifetime.</summary>
    public void Set(string query, ExternalFoodItem item)
    {
        if (_entries.Count >= MaxEntries && !_entries.ContainsKey(query))
            _entries.Clear();

        _entries[query] = new CacheEntry(item, _timeProvider.GetUtcNow().Add(EntryLifetime));
    }

    /// <summary>A generated meal kept briefly, so repeated requests for the same day agree.</summary>
    private sealed record CacheEntry(ExternalFoodItem Item, DateTimeOffset ExpiresAtUtc);
}

/// <summary>
/// Generates portion-balanced meals from live open-source USDA FoodData Central foods via
/// <see cref="IExternalFoodSource"/>, enforcing <see cref="GeneratedMealPortionRule"/> calorie and oil caps.
/// </summary>
public sealed class OpenSourceMealGenerator(
    IExternalFoodSource externalFoodSource,
    OpenSourceIngredientCache? cache = null)
{
    public const string OpenSourceMatchReason = "USDA FoodData Central";
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(5);
    private const int SuggestionsPerMealType = 3;

    /// <summary>A slot of a generated meal with the ingredients chosen for it.</summary>
    private sealed record OpenSourceIngredientSlot(
        string DisplayName,
        string GenericQuery,
        float ReferenceGrams,
        bool IsOil = false);

    /// <summary>The structure a generated meal is filled from.</summary>
    private sealed record OpenSourceMealBlueprint(
        string ConceptName,
        string Description,
        IReadOnlyList<OpenSourceIngredientSlot> Slots);

    private static readonly OpenSourceMealBlueprint[] SavouryBreakfastBlueprints =
    [
        new("Spinach & Egg Power Plate",
            "Scrambled eggs with whole-grain bread, fresh spinach, and olive oil (USDA FoodData Central).",
            [
                new("Eggs", "eggs", 110f),
                new("Whole-Grain Bread", "whole bread", 65f),
                new("Spinach", "spinach", 45f),
                new("Olive Oil", "olive oil", 7f, IsOil: true)
            ]),
        new("Avocado Tofu Scramble",
            "Seasoned tofu scramble with bell peppers, avocado, and olive oil (USDA FoodData Central).",
            [
                new("Tofu", "tofu", 145f),
                new("Bell Peppers", "peppers", 75f),
                new("Avocado", "avocado", 60f),
                new("Olive Oil", "olive oil", 6f, IsOil: true)
            ]),
        new("Smoked Salmon & Tomato Toast",
            "Salmon with whole-grain bread, tomatoes, and cucumber (USDA FoodData Central).",
            [
                new("Salmon", "salmon", 110f),
                new("Whole-Grain Bread", "whole bread", 70f),
                new("Tomatoes", "tomatoes", 80f),
                new("Cucumber", "cucumber", 60f)
            ]),
        new("Turkey & Mushroom Egg Skillet",
            "Eggs with lean turkey, mushrooms, and spinach (USDA FoodData Central).",
            [
                new("Eggs", "eggs", 100f),
                new("Turkey", "turkey", 75f),
                new("Mushrooms", "mushrooms", 70f),
                new("Spinach", "spinach", 45f)
            ])
    ];

    private static readonly OpenSourceMealBlueprint[] SweetBreakfastBlueprints =
    [
        new("Blueberry Greek Yogurt Oats",
            "Rolled oats with Greek yogurt, blueberries, and walnuts (USDA FoodData Central).",
            [
                new("Rolled Oats", "oats", 65f),
                new("Greek Yogurt", "greek yogurt", 170f),
                new("Blueberries", "blueberries", 80f),
                new("Walnuts", "walnuts", 15f)
            ]),
        new("Banana Peanut Butter Oatmeal",
            "Warm oatmeal topped with sliced banana and creamy peanut butter (USDA FoodData Central).",
            [
                new("Oatmeal", "oats", 65f),
                new("Banana", "banana", 110f),
                new("Peanut Butter", "peanut butter", 20f),
                new("Greek Yogurt", "greek yogurt", 120f)
            ]),
        new("Cottage Cheese Berry Bowl",
            "Cottage cheese layered with strawberries, raspberries, almonds, and honey (USDA FoodData Central).",
            [
                new("Cottage Cheese", "cottage cheese", 200f),
                new("Strawberries", "strawberries", 90f),
                new("Raspberries", "raspberries", 60f),
                new("Almonds", "almonds", 18f),
                new("Honey", "honey", 12f)
            ]),
        new("Kefir Mango & Granola Bowl",
            "Kefir paired with sweet mango, rolled oats, and pumpkin seeds (USDA FoodData Central).",
            [
                new("Kefir", "kefir", 220f),
                new("Mango", "mango", 110f),
                new("Rolled Oats", "oats", 50f),
                new("Almonds", "almonds", 15f)
            ])
    ];

    private static readonly OpenSourceMealBlueprint[] LunchBlueprints =
    [
        new("Chicken Quinoa Broccoli Bowl",
            "Roasted chicken breast with quinoa, broccoli, and olive oil (USDA FoodData Central).",
            [
                new("Chicken Breast", "chicken breast", 145f),
                new("Quinoa", "quinoa", 140f),
                new("Broccoli", "broccoli", 95f),
                new("Olive Oil", "olive oil", 7f, IsOil: true)
            ]),
        new("Tuna Brown Rice Avocado Bowl",
            "Light tuna with brown rice, crisp cucumber, and avocado (USDA FoodData Central).",
            [
                new("Tuna", "tuna", 145f),
                new("Brown Rice", "brown rice", 130f),
                new("Cucumber", "cucumber", 80f),
                new("Avocado", "avocado", 60f)
            ]),
        new("Lentil Sweet Potato Tahini Bowl",
            "Cooked lentils with baked sweet potato, spinach, and tahini (USDA FoodData Central).",
            [
                new("Lentils", "lentils", 160f),
                new("Sweet Potato", "sweet potato", 150f),
                new("Spinach", "spinach", 60f),
                new("Tahini", "tahini", 20f)
            ]),
        new("Tempeh Edamame Rice Bowl",
            "Tempeh with edamame, brown rice, carrots, and olive oil (USDA FoodData Central).",
            [
                new("Tempeh", "tempeh", 130f),
                new("Edamame", "edamame", 90f),
                new("Brown Rice", "brown rice", 125f),
                new("Carrots", "carrots", 70f),
                new("Olive Oil", "olive oil", 6f, IsOil: true)
            ])
    ];

    private static readonly OpenSourceMealBlueprint[] DinnerBlueprints =
    [
        new("Baked Salmon & Sweet Potato Plate",
            "Salmon fillet with baked sweet potato, asparagus, and olive oil (USDA FoodData Central).",
            [
                new("Salmon", "salmon", 155f),
                new("Sweet Potato", "sweet potato", 165f),
                new("Asparagus", "asparagus", 110f),
                new("Olive Oil", "olive oil", 6f, IsOil: true)
            ]),
        new("Turkey Bean & Rice Skillet",
            "Lean turkey with black beans, tomatoes, and brown rice (USDA FoodData Central).",
            [
                new("Turkey", "turkey", 145f),
                new("Beans", "beans", 100f),
                new("Tomatoes", "tomatoes", 90f),
                new("Brown Rice", "brown rice", 125f)
            ]),
        new("Baked Cod Quinoa & Zucchini",
            "Cod fillet with cooked quinoa, zucchini, and olive oil (USDA FoodData Central).",
            [
                new("Cod", "cod", 165f),
                new("Quinoa", "quinoa", 145f),
                new("Zucchini", "zucchini", 110f),
                new("Olive Oil", "olive oil", 8f, IsOil: true)
            ]),
        new("Tofu Edamame Broccoli Stir-Fry",
            "Firm tofu with edamame, broccoli, bell peppers, and rice (USDA FoodData Central).",
            [
                new("Tofu", "tofu", 145f),
                new("Edamame", "edamame", 95f),
                new("Broccoli", "broccoli", 95f),
                new("Rice", "rice", 125f),
                new("Olive Oil", "olive oil", 6f, IsOil: true)
            ])
    ];

    private static readonly OpenSourceMealBlueprint[] SnackBlueprints =
    [
        new("Apple & Peanut Butter",
            "Fresh apple paired with peanut butter (USDA FoodData Central).",
            [
                new("Apple", "apple", 165f),
                new("Peanut Butter", "peanut butter", 24f)
            ]),
        new("Cottage Cheese & Berries",
            "Cottage cheese topped with blueberries and raspberries (USDA FoodData Central).",
            [
                new("Cottage Cheese", "cottage cheese", 180f),
                new("Blueberries", "blueberries", 70f),
                new("Raspberries", "raspberries", 60f)
            ]),
        new("Greek Yogurt & Almonds",
            "Greek yogurt with roasted almonds and strawberries (USDA FoodData Central).",
            [
                new("Greek Yogurt", "greek yogurt", 175f),
                new("Almonds", "almonds", 20f),
                new("Strawberries", "strawberries", 80f)
            ]),
        new("Hummus & Crisp Veggies",
            "Creamy hummus with fresh carrots and cucumber (USDA FoodData Central).",
            [
                new("Hummus", "hummus", 90f),
                new("Carrots", "carrots", 90f),
                new("Cucumber", "cucumber", 90f)
            ])
    ];

    private readonly OpenSourceIngredientCache _cache = cache ?? new OpenSourceIngredientCache();

    /// <summary>Builds one meal suggestion for the slot.</summary>
    public async Task<MealSuggestionDto?> GenerateMealAsync(
        string mealType,
        string breakfastPreference,
        float targetCalories,
        float targetFats,
        string? excludeConceptName = null,
        int seedIndex = 0,
        CancellationToken cancellationToken = default)
    {
        if (!externalFoodSource.IsConfigured ||
            !float.IsFinite(targetCalories) || targetCalories <= 0f ||
            !float.IsFinite(targetFats) || targetFats < 0f)
            return null;

        var blueprints = GetBlueprints(mealType, breakfastPreference);
        if (blueprints.Length == 0)
            return null;

        var ordered = OrderBlueprints(blueprints, excludeConceptName, seedIndex);
        foreach (var blueprint in ordered)
        {
            var built = await TryBuildSuggestionAsync(blueprint, targetCalories, targetFats, cancellationToken)
                .ConfigureAwait(false);
            if (built is not null)
                return built;
        }

        return null;
    }

    /// <summary>Builds the day's meal suggestions.</summary>
    public async Task<IReadOnlyList<MealSuggestionDto>> GenerateSuggestionsAsync(
        string mealType,
        string breakfastPreference,
        float targetCalories,
        float targetFats,
        IReadOnlySet<string>? excludeConceptNames = null,
        CancellationToken cancellationToken = default)
    {
        if (!externalFoodSource.IsConfigured ||
            !float.IsFinite(targetCalories) || targetCalories <= 0f ||
            !float.IsFinite(targetFats) || targetFats < 0f)
            return [];

        var blueprints = GetBlueprints(mealType, breakfastPreference)
            .Where(blueprint => excludeConceptNames is null || !excludeConceptNames.Contains(blueprint.ConceptName))
            .Take(SuggestionsPerMealType)
            .ToArray();
        if (blueprints.Length == 0)
            return [];

        var tasks = blueprints.Select(blueprint =>
            TryBuildSuggestionAsync(blueprint, targetCalories, targetFats, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.OfType<MealSuggestionDto>().ToList();
    }

    /// <summary>Tries to build a suggestion from the slot's ingredients, or nothing.</summary>
    private async Task<MealSuggestionDto?> TryBuildSuggestionAsync(
        OpenSourceMealBlueprint blueprint,
        float targetCalories,
        float targetFats,
        CancellationToken cancellationToken)
    {
        using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookupCts.CancelAfter(LookupTimeout);

        var resolvedTasks = blueprint.Slots
            .Select(slot => ResolveSlotAsync(slot, lookupCts.Token))
            .ToArray();
        var resolved = await Task.WhenAll(resolvedTasks).ConfigureAwait(false);

        var profiles = new List<GeneratedMealIngredientProfile>(blueprint.Slots.Count);
        var usedExternalSource = false;
        foreach (var entry in resolved)
        {
            if (entry.Profile is null)
                return null;
            if (entry.FromExternalSource)
                usedExternalSource = true;
            profiles.Add(entry.Profile);
        }

        if (!usedExternalSource)
            return null;

        var portions = GeneratedMealPortionRule.Calculate(profiles, targetCalories, targetFats);
        if (portions.Count == 0)
            return null;

        var components = portions.Select(portion => new MealSuggestionComponentDto(
            0,
            portion.Profile.Name,
            portion.Profile.IsOil ? "Sauce" : "Ingredient",
            portion.Profile.IsOil ? "SauceOnSide" : "Base",
            portion.Grams,
            false,
            portion.Profile.CaloriesPer100Grams,
            portion.Profile.ProteinPer100Grams,
            portion.Profile.CarbsPer100Grams,
            portion.Profile.FatsPer100Grams)).ToList();
        var totals = MacroNutrients.Sum(portions.Select(portion => portion.Nutrients));
        var group = new MealSuggestionComponentGroupDto("Ingredients", "Food24", components);

        return new MealSuggestionDto(
            blueprint.ConceptName,
            blueprint.Description,
            [],
            components,
            [group],
            totals.Calories,
            totals.Protein,
            totals.Carbs,
            totals.Fats,
            1.0f,
            OpenSourceMatchReason,
            "Food24",
            blueprint.Description);
    }

    /// <summary>Resolves the ingredient profile for a slot, from the catalog or an external source.</summary>
    private async Task<(GeneratedMealIngredientProfile? Profile, bool FromExternalSource)> ResolveSlotAsync(
        OpenSourceIngredientSlot slot,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = GenericFoodQueryRule.NormalizeOrEmpty(slot.GenericQuery);
        if (normalizedQuery.Length > 0)
        {
            if (_cache.TryGet(normalizedQuery, out var cached))
                return (ToProfile(slot, cached), true);

            try
            {
                var matches = await externalFoodSource.SearchAsync(normalizedQuery, cancellationToken)
                    .ConfigureAwait(false);
                var best = SelectBestMatch(normalizedQuery, matches);
                if (best is not null)
                {
                    _cache.Set(normalizedQuery, best);
                    return (ToProfile(slot, best), true);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        var fallback = GeneratedMealIngredientCatalog.Resolve([slot.DisplayName]);
        return fallback.Count == 1
            ? (fallback[0] with { ReferenceGrams = slot.ReferenceGrams, IsOil = slot.IsOil }, false)
            : (null, false);
    }

    /// <summary>Turns an external food into the generator's ingredient profile.</summary>
    private static GeneratedMealIngredientProfile ToProfile(OpenSourceIngredientSlot slot, ExternalFoodItem item) =>
        new(
            slot.DisplayName,
            slot.ReferenceGrams,
            item.CaloriesPer100Grams,
            item.ProteinPer100Grams,
            item.CarbsPer100Grams,
            item.FatsPer100Grams,
            slot.IsOil);

    /// <summary>Picks the candidate that best fits the slot, or null when none is close enough.</summary>
    private static ExternalFoodItem? SelectBestMatch(string query, IReadOnlyList<ExternalFoodItem> items)
    {
        if (items.Count == 0)
            return null;

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return items
            .Where(item =>
                item.FdcId > 0 &&
                !string.IsNullOrWhiteSpace(item.Name) &&
                float.IsFinite(item.CaloriesPer100Grams) && item.CaloriesPer100Grams >= 0f &&
                float.IsFinite(item.ProteinPer100Grams) && item.ProteinPer100Grams >= 0f &&
                float.IsFinite(item.CarbsPer100Grams) && item.CarbsPer100Grams >= 0f &&
                float.IsFinite(item.FatsPer100Grams) && item.FatsPer100Grams >= 0f)
            .OrderByDescending(item => ScoreMatch(item, tokens))
            .ThenBy(item => item.Name.Length)
            .FirstOrDefault();
    }

    /// <summary>Scores how well a food fits a slot, so a poor fit is refused rather than substituted.</summary>
    private static int ScoreMatch(ExternalFoodItem item, string[] queryTokens)
    {
        var score = 0;
        var lowerName = item.Name.ToLowerInvariant();
        if (queryTokens.All(token => lowerName.Contains(token, StringComparison.Ordinal)))
            score += 40;
        if (lowerName.StartsWith(queryTokens[0], StringComparison.Ordinal))
            score += 20;
        if (string.Equals(item.DataType, "SR Legacy", StringComparison.OrdinalIgnoreCase))
            score += 15;
        else if (string.Equals(item.DataType, "Foundation", StringComparison.OrdinalIgnoreCase))
            score += 12;
        if (lowerName.Contains("breaded", StringComparison.Ordinal) ||
            lowerName.Contains("fried", StringComparison.Ordinal) ||
            lowerName.Contains("dressing", StringComparison.Ordinal) ||
            lowerName.Contains("candy", StringComparison.Ordinal) ||
            lowerName.Contains("fast food", StringComparison.Ordinal))
            score -= 25;
        return score;
    }

    /// <summary>The blueprint set for the requested meal, which bounds what the generator may choose.</summary>
    private static OpenSourceMealBlueprint[] GetBlueprints(string mealType, string breakfastPreference)
    {
        var isSweet = string.Equals(breakfastPreference, "Sweet", StringComparison.OrdinalIgnoreCase);
        return mealType.ToLowerInvariant() switch
        {
            "breakfast" => isSweet ? SweetBreakfastBlueprints : SavouryBreakfastBlueprints,
            "lunch" => LunchBlueprints,
            "dinner" => DinnerBlueprints,
            "snack" => SnackBlueprints,
            _ => LunchBlueprints
        };
    }

    /// <summary>Orders the meal blueprints so the most suitable comes first.</summary>
    private static IReadOnlyList<OpenSourceMealBlueprint> OrderBlueprints(
        OpenSourceMealBlueprint[] blueprints,
        string? excludeConceptName,
        int seedIndex)
    {
        var startIndex = 0;
        if (!string.IsNullOrWhiteSpace(excludeConceptName))
        {
            var matchedIndex = Array.FindIndex(blueprints, blueprint =>
                string.Equals(blueprint.ConceptName, excludeConceptName, StringComparison.OrdinalIgnoreCase));
            startIndex = matchedIndex >= 0
                ? (matchedIndex + 1) % blueprints.Length
                : Math.Abs(seedIndex) % blueprints.Length;
        }
        else
        {
            startIndex = Math.Abs(seedIndex) % blueprints.Length;
        }

        var ordered = new List<OpenSourceMealBlueprint>(blueprints.Length);
        for (var offset = 0; offset < blueprints.Length; offset++)
            ordered.Add(blueprints[(startIndex + offset) % blueprints.Length]);
        return ordered;
    }
}
