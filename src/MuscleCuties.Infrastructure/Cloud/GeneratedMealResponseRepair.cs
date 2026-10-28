using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Cloud;

internal static class GeneratedMealResponseRepair
{
    private const float LegacyEqualSplitTolerance = 0.001f;

    public static MealSuggestionDto Repair(
        MealSuggestionDto suggestion,
        float targetCalories,
        float targetFats)
    {
        if (!HasLegacyEqualSplit(suggestion.Components))
            return suggestion;

        var profiles = GeneratedMealIngredientCatalog.Resolve(
            suggestion.Components.Select(component => component.Name).ToArray());
        var portions = GeneratedMealPortionRule.Calculate(profiles, targetCalories, targetFats);
        if (portions.Count != suggestion.Components.Count)
            throw new InvalidOperationException("Generated meal ingredients have no reference nutrition profile.");

        var byName = portions.ToDictionary(portion => portion.Profile.Name, StringComparer.Ordinal);
        var components = suggestion.Components.Select(component =>
            ApplyPortion(component, byName[component.Name])).ToArray();
        var groups = suggestion.ComponentGroups.Select(group => group with
        {
            Items = group.Items.Select(component =>
                ApplyPortion(component, byName[component.Name])).ToArray()
        }).ToArray();
        var totals = MacroNutrients.Sum(portions.Select(portion => portion.Nutrients));

        return suggestion with
        {
            Components = components,
            ComponentGroups = groups,
            Calories = totals.Calories,
            Protein = totals.Protein,
            Carbs = totals.Carbs,
            Fats = totals.Fats
        };
    }

    private static MealSuggestionComponentDto ApplyPortion(
        MealSuggestionComponentDto component,
        GeneratedMealIngredientPortion portion) => component with
    {
        Grams = portion.Grams,
        Calories = portion.Profile.CaloriesPer100Grams,
        Protein = portion.Profile.ProteinPer100Grams,
        Carbs = portion.Profile.CarbsPer100Grams,
        Fats = portion.Profile.FatsPer100Grams
    };

    private static bool HasLegacyEqualSplit(IReadOnlyList<MealSuggestionComponentDto> components)
    {
        if (components.Count < 2 || components.Any(component => component.FoodItemId != 0))
            return false;

        var nonOil = components.Where(component =>
            !component.Name.EndsWith(" Oil", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nonOil.Length < 2)
            return false;

        var first = nonOil[0];
        return nonOil.Skip(1).All(component =>
            Close(component.Grams, first.Grams) &&
            Close(component.Calories, first.Calories) &&
            Close(component.Protein, first.Protein) &&
            Close(component.Carbs, first.Carbs) &&
            Close(component.Fats, first.Fats));
    }

    private static bool Close(float left, float right) =>
        float.IsFinite(left) && float.IsFinite(right) &&
        MathF.Abs(left - right) <= LegacyEqualSplitTolerance;
}
