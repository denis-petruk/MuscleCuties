namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>A recipe being edited, which enforces its own field limits before it can be saved.</summary>
public sealed class RecipeDraft
{
    public const int MaxNameLength = 160;
    public const int MaxDescriptionLength = 1000;
    public const int MaxIngredientNameLength = 160;
    public const int MinimumIngredientCount = 1;
    public const float MinIngredientGrams = 0f;
    public const float MinNutrientValue = 0f;

    /// <summary>Holds a recipe together with the nutrition its ingredients add up to.</summary>
    private RecipeDraft(string name, string? description, IReadOnlyList<RecipeIngredient> ingredients,
        RecipeNutrientSummary nutrition, float? cookedWeightGrams)
    {
        Name = name;
        Description = description;
        Ingredients = ingredients;
        Nutrition = nutrition;
        CookedWeightGrams = cookedWeightGrams;
    }

    /// <summary>Name the user gave the recipe.</summary>
    public string Name { get; }
    /// <summary>Optional note stored with the recipe.</summary>
    public string? Description { get; }
    /// <summary>Ingredients with the grams the user entered.</summary>
    public IReadOnlyList<RecipeIngredient> Ingredients { get; }
    /// <summary>Nutrition the ingredients add up to.</summary>
    public RecipeNutrientSummary Nutrition { get; }
    /// <summary>Weight after cooking, when the user recorded one.</summary>
    public float? CookedWeightGrams { get; }

    /// <summary>Shows one portion for the requested servings or weight.</summary>
    public RecipePortion PreviewPortion(float? requestedServingsCount, float? requestedGrams) =>
        RecipeBatchCalculationRule.CalculatePortion(new RecipeBatchTotals(
            Nutrition.ServingsCount, Nutrition.TotalRawWeightGrams, CookedWeightGrams,
            Nutrition.TotalCalories, Nutrition.TotalProtein, Nutrition.TotalCarbs,
            Nutrition.TotalNetCarbs, Nutrition.TotalFats), requestedServingsCount, requestedGrams);

    /// <summary>Validates the recipe inputs and builds the draft with its nutrition.</summary>
    public static RecipeDraft Create(string name, string? description,
        IReadOnlyList<RecipeIngredient>? ingredients, float servingsCount, float? cookedWeightGrams)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException("Enter a recipe name within the supported length.", nameof(name));
        if (description?.Length > MaxDescriptionLength)
            throw new ArgumentException("Recipe description is too long.", nameof(description));
        if (ingredients is null || ingredients.Count < MinimumIngredientCount)
            throw new ArgumentException("Add at least one ingredient.", nameof(ingredients));
        if (!float.IsFinite(servingsCount) || servingsCount < RecipeBatchCalculationRule.MinServingsCount ||
            servingsCount > RecipeBatchCalculationRule.MaxServingsCount)
            throw new ArgumentOutOfRangeException(nameof(servingsCount));
        if (cookedWeightGrams.HasValue &&
            (!float.IsFinite(cookedWeightGrams.Value) || cookedWeightGrams.Value <= MinIngredientGrams))
            throw new ArgumentOutOfRangeException(nameof(cookedWeightGrams));

        foreach (var ingredient in ingredients)
        {
            if (ingredient is null || string.IsNullOrWhiteSpace(ingredient.Name) ||
                ingredient.Name.Trim().Length > MaxIngredientNameLength ||
                !float.IsFinite(ingredient.Grams) || ingredient.Grams <= MinIngredientGrams ||
                !ValidNutrient(ingredient.Calories) || !ValidNutrient(ingredient.Protein) ||
                !ValidNutrient(ingredient.Carbs) || !ValidNutrient(ingredient.Fats) ||
                !ValidNutrient(ingredient.Fiber) || !ValidNutrient(ingredient.SugarAlcohols))
                throw new ArgumentException("Recipe ingredients need a name, positive weight and finite nutrition.",
                    nameof(ingredients));
        }

        var nutrition = RecipeBatchCalculationRule.Calculate(ingredients, servingsCount, cookedWeightGrams);
        if (!float.IsFinite(nutrition.TotalCalories) || !float.IsFinite(nutrition.TotalRawWeightGrams) ||
            nutrition.TotalRawWeightGrams <= MinIngredientGrams)
            throw new ArgumentException("Recipe totals exceed supported values.", nameof(ingredients));

        return new RecipeDraft(name.Trim(), description?.Trim(), ingredients.ToArray(), nutrition,
            cookedWeightGrams);
    }

    /// <summary>True when the nutrient value is finite and not negative.</summary>
    private static bool ValidNutrient(float value) => float.IsFinite(value) && value >= MinNutrientValue;
}
