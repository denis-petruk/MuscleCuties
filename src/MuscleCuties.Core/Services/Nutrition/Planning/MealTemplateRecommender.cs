using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

internal static class MealTemplateRecommender
{
    private const int RecommendationCount = 4;
    private static readonly string[] MeatOrFishTerms =
    [
        "chicken",
        "salmon",
        "tuna",
        "turkey",
        "beef",
        "shrimp",
        "cod",
        "pork"
    ];

    private static readonly IReadOnlyList<MealType> MealOrder =
    [
        MealType.Breakfast,
        MealType.Lunch,
        MealType.Dinner,
        MealType.Snack
    ];

    public static List<MealTemplate> Recommend(
        IEnumerable<MealTemplate> templates,
        UserProfile? profile,
        NutritionPlan plan,
        CyclePhase phase)
    {
        var userDietaryTags = ParseDietaryTags(profile?.DietaryTags);
        var preferMeatOrFish = userDietaryTags.Count == 0;
        var candidates = templates
            .Where(template => template.Entries.Count > 0)
            .Where(template => IsCompatibleWithDiet(template, userDietaryTags))
            .ToList();

        if (candidates.Count == 0)
            return [];

        var selected = SelectBestDailySet(candidates, plan, phase, preferMeatOrFish);

        if (selected.Count < RecommendationCount)
        {
            selected.AddRange(candidates
                .Where(template => selected.All(existing => existing.Id != template.Id))
                .OrderByDescending(template => ScoreTemplate(template, plan, phase, preferMeatOrFish))
                .ThenBy(template => template.SortOrder)
                .ThenBy(template => template.Name)
                .Take(RecommendationCount - selected.Count));
        }

        return selected
            .OrderBy(template => GetMealOrderIndex(template.MealType))
            .ThenBy(template => template.SortOrder)
            .Take(RecommendationCount)
            .ToList();
    }

    private static int GetMealOrderIndex(MealType mealType)
    {
        for (var index = 0; index < MealOrder.Count; index++)
        {
            if (MealOrder[index] == mealType)
                return index;
        }

        return MealOrder.Count;
    }

    private static List<MealTemplate> SelectBestDailySet(
        IReadOnlyCollection<MealTemplate> candidates,
        NutritionPlan plan,
        CyclePhase phase,
        bool preferMeatOrFish)
    {
        var candidateGroups = MealOrder
            .Select(mealType => candidates
                .Where(template => template.MealType == mealType)
                .OrderByDescending(template => ScoreTemplate(template, plan, phase, preferMeatOrFish))
                .ThenBy(template => template.SortOrder)
                .ThenBy(template => template.Name)
                .Take(5)
                .ToList())
            .Where(group => group.Count > 0)
            .ToList();

        if (candidateGroups.Count == 0)
            return [];

        var combinations = new List<List<MealTemplate>> { new() };
        foreach (var group in candidateGroups)
        {
            combinations = combinations
                .SelectMany(current => group.Select(template =>
                {
                    var next = new List<MealTemplate>(current) { template };
                    return next;
                }))
                .ToList();
        }

        return combinations
            .OrderByDescending(combination => ScoreDailySet(combination, plan, phase, preferMeatOrFish))
            .ThenBy(combination => combination.Sum(template => template.SortOrder))
            .First();
    }

    private static bool IsCompatibleWithDiet(
        MealTemplate template,
        IReadOnlySet<DietaryTag> userDietaryTags)
    {
        if (userDietaryTags.Count == 0)
            return true;

        var templateTags = ParseDietaryTags(template.DietaryTags);
        if (templateTags.Count == 0)
            return false;

        if (userDietaryTags.Contains(DietaryTag.Vegan) &&
            !templateTags.Contains(DietaryTag.Vegan))
        {
            return false;
        }

        if (userDietaryTags.Contains(DietaryTag.Vegetarian) &&
            !templateTags.Contains(DietaryTag.Vegetarian) &&
            !templateTags.Contains(DietaryTag.Vegan))
        {
            return false;
        }

        if (userDietaryTags.Contains(DietaryTag.GlutenFree) &&
            !templateTags.Contains(DietaryTag.GlutenFree))
        {
            return false;
        }

        if (userDietaryTags.Contains(DietaryTag.LactoseFree) &&
            !templateTags.Contains(DietaryTag.LactoseFree) &&
            !templateTags.Contains(DietaryTag.Vegan))
        {
            return false;
        }

        return true;
    }

    private static float ScoreTemplate(
        MealTemplate template,
        NutritionPlan plan,
        CyclePhase phase,
        bool preferMeatOrFish)
    {
        var mealTarget = plan.Meals.FirstOrDefault(meal => meal.MealType == template.MealType);
        var macros = CalculateMacros(template);

        return ScorePhaseFit(template, phase) +
               (preferMeatOrFish && IsMeatOrFishTemplate(template) ? 18f : 0f) +
               ScoreMacroFit(macros, mealTarget) +
               ScoreMicronutrientCoverage(template, plan.Goals);
    }

    private static float ScoreDailySet(
        IReadOnlyCollection<MealTemplate> templates,
        NutritionPlan plan,
        CyclePhase phase,
        bool preferMeatOrFish)
    {
        var macros = MacroNutrients.Sum(templates.Select(CalculateMacros));
        return 85f * Closeness(macros.Calories, plan.Calories) +
               70f * MinimumCoverage(macros.Protein, plan.Protein) +
               45f * Closeness(macros.Carbs, plan.Carbs) +
               45f * Closeness(macros.Fats, plan.Fats) +
               (preferMeatOrFish && templates.Any(IsMeatOrFishTemplate) ? 65f : 0f) +
               templates.Sum(template => ScorePhaseFit(template, phase)) +
               ScoreDailyMicronutrientCoverage(templates, plan.Goals);
    }

    private static bool IsMeatOrFishTemplate(MealTemplate template) =>
        ContainsAny(template.Name, MeatOrFishTerms) ||
        template.Entries.Any(entry =>
            entry.FoodItem is not null &&
            ContainsAny(entry.FoodItem.Name, MeatOrFishTerms));

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static float ScorePhaseFit(MealTemplate template, CyclePhase phase)
    {
        var phaseTags = ParseTags<CyclePhase>(template.PhaseTags);
        if (phaseTags.Count == 0)
            return 12f;

        return phaseTags.Contains(phase) ? 80f : 0f;
    }

    private static MacroNutrients CalculateMacros(MealTemplate template) =>
        MacroNutrients.Sum(template.Entries
            .Where(entry => entry.FoodItem is not null)
            .Select(entry => MacroNutrients.FromFood(entry.FoodItem!, entry.Grams)));

    private static float ScoreMacroFit(
        MacroNutrients macros,
        MealNutritionTarget? target)
    {
        if (target is null || target.Calories <= 0f)
            return 0f;

        return 35f * Closeness(macros.Calories, target.Calories) +
               30f * MinimumCoverage(macros.Protein, target.Protein) +
               15f * Closeness(macros.Carbs, target.Carbs) +
               15f * Closeness(macros.Fats, target.Fats);
    }

    private static float ScoreMicronutrientCoverage(
        MealTemplate template,
        ProfileNutritionGoals goals)
    {
        var entries = template.Entries.Where(entry => entry.FoodItem is not null).ToList();
        if (entries.Count == 0)
            return 0f;

        var fat = entries.Sum(entry => entry.FoodItem!.Fats * entry.Grams / 100f);
        var countsFatSolubleVitamins = fat >= FatSolubleVitaminAbsorption.MinimumFatGramsInWindow;

        return 8f * NutrientCoverage(entries, goals.Fiber, food => food.Fiber) +
               8f * NutrientCoverage(entries, goals.Iron, food => food.Iron) +
               7f * NutrientCoverage(entries, goals.VitaminB12, food => food.VitaminB12) +
               7f * NutrientCoverage(entries, goals.VitaminC, food => food.VitaminC) +
               7f * NutrientCoverage(entries, goals.VitaminB6, food => food.VitaminB6) +
               7f * NutrientCoverage(entries, goals.Folate, food => food.Folate) +
               7f * NutrientCoverage(entries, goals.Calcium, food => food.Calcium) +
               7f * NutrientCoverage(entries, goals.Magnesium, food => food.Magnesium) +
               7f * NutrientCoverage(entries, goals.Zinc, food => food.Zinc) +
               7f * NutrientCoverage(entries, goals.Potassium, food => food.Potassium) +
               (countsFatSolubleVitamins ? 7f * NutrientCoverage(entries, goals.VitaminA, food => food.VitaminA) : 0f) +
               (countsFatSolubleVitamins ? 7f * NutrientCoverage(entries, goals.VitaminD, food => food.VitaminD) : 0f);
    }

    private static float ScoreDailyMicronutrientCoverage(
        IReadOnlyCollection<MealTemplate> templates,
        ProfileNutritionGoals goals)
    {
        var entries = templates
            .SelectMany(template => template.Entries)
            .Where(entry => entry.FoodItem is not null)
            .ToList();
        var absorbableFatSolubleEntries = templates
            .Where(HasEnoughTemplateFat)
            .SelectMany(template => template.Entries)
            .Where(entry => entry.FoodItem is not null)
            .ToList();

        return 12f * NutrientCoverage(entries, goals.Fiber, food => food.Fiber) +
               10f * NutrientCoverage(entries, goals.Iron, food => food.Iron) +
               10f * NutrientCoverage(entries, goals.VitaminB12, food => food.VitaminB12) +
               10f * NutrientCoverage(entries, goals.VitaminC, food => food.VitaminC) +
               10f * NutrientCoverage(entries, goals.VitaminB6, food => food.VitaminB6) +
               10f * NutrientCoverage(entries, goals.Folate, food => food.Folate) +
               10f * NutrientCoverage(entries, goals.Calcium, food => food.Calcium) +
               10f * NutrientCoverage(entries, goals.Magnesium, food => food.Magnesium) +
               10f * NutrientCoverage(entries, goals.Zinc, food => food.Zinc) +
               10f * NutrientCoverage(entries, goals.Potassium, food => food.Potassium) +
               10f * NutrientCoverage(absorbableFatSolubleEntries, goals.VitaminA, food => food.VitaminA) +
               10f * NutrientCoverage(absorbableFatSolubleEntries, goals.VitaminD, food => food.VitaminD);
    }

    private static bool HasEnoughTemplateFat(MealTemplate template) =>
        template.Entries
            .Where(entry => entry.FoodItem is not null)
            .Sum(entry => entry.FoodItem!.Fats * entry.Grams / 100f) >=
        FatSolubleVitaminAbsorption.MinimumFatGramsInWindow;

    private static float NutrientCoverage(
        IEnumerable<MealTemplateEntry> entries,
        float? goal,
        Func<FoodItem, float> valueSelector)
    {
        if (goal is not > 0f)
            return 0f;

        var amount = entries.Sum(entry => valueSelector(entry.FoodItem!) * entry.Grams / 100f);
        return MinimumCoverage(amount, goal.Value);
    }

    private static float Closeness(float actual, float target)
    {
        if (target <= 0f)
            return 0f;

        return 1f - Math.Clamp(Math.Abs(actual - target) / target, 0f, 1f);
    }

    private static float MinimumCoverage(float actual, float target)
    {
        if (target <= 0f)
            return 0f;

        return Math.Clamp(actual / target, 0f, 1f);
    }

    private static IReadOnlySet<T> ParseTags<T>(string? value)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<T>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<T>(part, ignoreCase: true, out var tag) ? tag : (T?)null)
            .Where(tag => tag.HasValue)
            .Select(tag => tag!.Value)
            .ToHashSet();
    }

    private static IReadOnlySet<DietaryTag> ParseDietaryTags(string? value)
    {
        var tags = ParseTags<DietaryTag>(value);
        return tags.Count == 0
            ? tags
            : tags.Where(tag => tag is not DietaryTag.None).ToHashSet();
    }
}
