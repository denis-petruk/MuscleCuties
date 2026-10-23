namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record RecipeIngredient(
    Guid Id,
    string Name,
    float Grams,
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Fiber = RecipeBatchCalculationRule.ZeroBaseline,
    float SugarAlcohols = RecipeBatchCalculationRule.ZeroBaseline,
    Guid? FoodItemId = null,
    string? SourceKind = null,
    string? SourceLabel = null,
    string? SourceVersion = null
)
{
    private const float MinNutrientBaseline = 0f;

    public bool IsCatalogBacked => FoodItemId.HasValue && FoodItemId.Value != Guid.Empty;
    public float SafeGrams => SafeNonNegative(Grams);
    public float SafeCalories => SafeNonNegative(Calories);
    public float SafeProtein => SafeNonNegative(Protein);
    public float SafeCarbs => SafeNonNegative(Carbs);
    public float SafeFats => SafeNonNegative(Fats);
    public float SafeFiber => SafeNonNegative(Fiber);
    public float SafeSugarAlcohols => SafeNonNegative(SugarAlcohols);

    public float SafeNetCarbs => MathF.Max(MinNutrientBaseline, SafeCarbs - (SafeFiber + SafeSugarAlcohols));

    public RecipeIngredient ScaleQuantity(float newGrams)
    {
        if (!float.IsFinite(newGrams) || newGrams <= MinNutrientBaseline || SafeGrams <= MinNutrientBaseline)
            throw new ArgumentOutOfRangeException(nameof(newGrams), "Ingredient grams must be positive and finite.");

        var factor = (double)newGrams / SafeGrams;
        return this with
        {
            Grams = newGrams,
            Calories = (float)(SafeCalories * factor),
            Protein = (float)(SafeProtein * factor),
            Carbs = (float)(SafeCarbs * factor),
            Fats = (float)(SafeFats * factor),
            Fiber = (float)(SafeFiber * factor),
            SugarAlcohols = (float)(SafeSugarAlcohols * factor)
        };
    }

    public RecipeIngredient ConvertToManual(string? manualName = null) =>
        this with
        {
            Name = string.IsNullOrWhiteSpace(manualName) ? Name : manualName.Trim(),
            FoodItemId = null,
            SourceKind = "Manual",
            SourceLabel = null,
            SourceVersion = null
        };

    private static float SafeNonNegative(float value) =>
        float.IsFinite(value) ? MathF.Max(MinNutrientBaseline, value) : MinNutrientBaseline;
}

public sealed record RecipeNutrientSummary(
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalFats,
    float TotalFiber,
    float TotalSugarAlcohols,
    float TotalNetCarbs,
    float PerServingCalories,
    float PerServingProtein,
    float PerServingCarbs,
    float PerServingFats,
    float PerServingFiber,
    float PerServingSugarAlcohols,
    float PerServingNetCarbs,
    float PerServingGrams,
    float TotalRawWeightGrams,
    float CookedWeightGrams,
    float ServingsCount
);

public sealed record RecipeBatchTotals(
    float ServingsCount,
    float TotalRawWeightGrams,
    float? CookedWeightGrams,
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalNetCarbs,
    float TotalFats);

public sealed record RecipePortion(
    RecipeBatchTotals Batch,
    float SelectedServingsCount,
    float SelectedGrams,
    float Calories,
    float Protein,
    float Carbs,
    float NetCarbs,
    float Fats);

public static class RecipeBatchCalculationRule
{
    public const float MinServingsCount = 0.1f;
    public const float MaxServingsCount = 200f;
    public const float DefaultServingsCount = 1f;
    public const int MinimumIngredientCount = 1;
    public const float MinWeightGrams = 0.0f;
    public const float ZeroBaseline = 0.0f;

    public static RecipePortion CalculatePortion(RecipeBatchTotals batch, float? requestedServingsCount,
        float? requestedGrams)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (requestedServingsCount.HasValue == requestedGrams.HasValue)
            throw new ArgumentException("Choose either a serving count or a weight.");

        RequirePositiveFinite(batch.ServingsCount, nameof(batch.ServingsCount));
        RequirePositiveFinite(batch.TotalRawWeightGrams, nameof(batch.TotalRawWeightGrams));
        if (batch.CookedWeightGrams.HasValue)
            RequirePositiveFinite(batch.CookedWeightGrams.Value, nameof(batch.CookedWeightGrams));
        RequireNonNegativeFinite(batch.TotalCalories, nameof(batch.TotalCalories));
        RequireNonNegativeFinite(batch.TotalProtein, nameof(batch.TotalProtein));
        RequireNonNegativeFinite(batch.TotalCarbs, nameof(batch.TotalCarbs));
        RequireNonNegativeFinite(batch.TotalNetCarbs, nameof(batch.TotalNetCarbs));
        RequireNonNegativeFinite(batch.TotalFats, nameof(batch.TotalFats));

        var weightBasis = batch.CookedWeightGrams ?? batch.TotalRawWeightGrams;
        var scale = requestedServingsCount.HasValue
            ? (double)requestedServingsCount.Value / batch.ServingsCount
            : (double)requestedGrams!.Value / weightBasis;

        if (requestedServingsCount.HasValue)
            RequirePositiveFinite(requestedServingsCount.Value, nameof(requestedServingsCount));
        else
            RequirePositiveFinite(requestedGrams!.Value, nameof(requestedGrams));

        return new RecipePortion(batch,
            CheckedPositivePortionValue(batch.ServingsCount * scale, nameof(requestedServingsCount)),
            CheckedPositivePortionValue(weightBasis * scale, nameof(requestedGrams)),
            CheckedPortionValue(batch.TotalCalories * scale, nameof(batch.TotalCalories)),
            CheckedPortionValue(batch.TotalProtein * scale, nameof(batch.TotalProtein)),
            CheckedPortionValue(batch.TotalCarbs * scale, nameof(batch.TotalCarbs)),
            CheckedPortionValue(batch.TotalNetCarbs * scale, nameof(batch.TotalNetCarbs)),
            CheckedPortionValue(batch.TotalFats * scale, nameof(batch.TotalFats)));
    }

    private static float CheckedPortionValue(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value > float.MaxValue || value < ZeroBaseline)
            throw new ArgumentOutOfRangeException(parameterName, "Recipe portion exceeds supported values.");
        return (float)value;
    }

    private static float CheckedPositivePortionValue(double value, string parameterName)
    {
        var result = CheckedPortionValue(value, parameterName);
        if (result <= ZeroBaseline)
            throw new ArgumentOutOfRangeException(parameterName, "Recipe portion is too small to represent.");
        return result;
    }

    private static void RequirePositiveFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= ZeroBaseline)
            throw new ArgumentOutOfRangeException(parameterName, "Recipe portion values must be positive and finite.");
    }

    private static void RequireNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < ZeroBaseline)
            throw new ArgumentOutOfRangeException(parameterName, "Recipe totals must be nonnegative and finite.");
    }

    public static RecipeNutrientSummary Calculate(
        IReadOnlyList<RecipeIngredient>? ingredients,
        float servingsCount,
        float? cookedWeightGrams = null)
    {
        var safeServings = !float.IsFinite(servingsCount) || servingsCount < MinServingsCount
            ? DefaultServingsCount
            : MathF.Min(servingsCount, MaxServingsCount);

        if (ingredients is null || ingredients.Count < MinimumIngredientCount)
        {
            return new RecipeNutrientSummary(
                TotalCalories: ZeroBaseline,
                TotalProtein: ZeroBaseline,
                TotalCarbs: ZeroBaseline,
                TotalFats: ZeroBaseline,
                TotalFiber: ZeroBaseline,
                TotalSugarAlcohols: ZeroBaseline,
                TotalNetCarbs: ZeroBaseline,
                PerServingCalories: ZeroBaseline,
                PerServingProtein: ZeroBaseline,
                PerServingCarbs: ZeroBaseline,
                PerServingFats: ZeroBaseline,
                PerServingFiber: ZeroBaseline,
                PerServingSugarAlcohols: ZeroBaseline,
                PerServingNetCarbs: ZeroBaseline,
                PerServingGrams: ZeroBaseline,
                TotalRawWeightGrams: ZeroBaseline,
                CookedWeightGrams: ZeroBaseline,
                ServingsCount: safeServings
            );
        }

        var totalCalories = ZeroBaseline;
        var totalProtein = ZeroBaseline;
        var totalCarbs = ZeroBaseline;
        var totalFats = ZeroBaseline;
        var totalFiber = ZeroBaseline;
        var totalSugarAlcohols = ZeroBaseline;
        var totalNetCarbs = ZeroBaseline;
        var totalRawWeight = ZeroBaseline;

        for (var i = 0; i < ingredients.Count; i++)
        {
            var item = ingredients[i];
            if (item is null)
                return Calculate([], safeServings);
            totalCalories += item.SafeCalories;
            totalProtein += item.SafeProtein;
            totalCarbs += item.SafeCarbs;
            totalFats += item.SafeFats;
            totalFiber += item.SafeFiber;
            totalSugarAlcohols += item.SafeSugarAlcohols;
            totalNetCarbs += item.SafeNetCarbs;
            totalRawWeight += item.SafeGrams;
        }

        if (!float.IsFinite(totalCalories) || !float.IsFinite(totalProtein) ||
            !float.IsFinite(totalCarbs) || !float.IsFinite(totalFats) ||
            !float.IsFinite(totalFiber) || !float.IsFinite(totalSugarAlcohols) ||
            !float.IsFinite(totalNetCarbs) || !float.IsFinite(totalRawWeight))
            return Calculate([], safeServings);

        var effectiveCookedWeight = cookedWeightGrams.HasValue &&
                                    float.IsFinite(cookedWeightGrams.Value) &&
                                    cookedWeightGrams.Value > MinWeightGrams
            ? cookedWeightGrams.Value
            : totalRawWeight;

        var perServingCalories = totalCalories / safeServings;
        var perServingProtein = totalProtein / safeServings;
        var perServingCarbs = totalCarbs / safeServings;
        var perServingFats = totalFats / safeServings;
        var perServingFiber = totalFiber / safeServings;
        var perServingSugarAlcohols = totalSugarAlcohols / safeServings;
        var perServingNetCarbs = totalNetCarbs / safeServings;
        var perServingGrams = effectiveCookedWeight / safeServings;

        if (!float.IsFinite(perServingCalories) || !float.IsFinite(perServingProtein) ||
            !float.IsFinite(perServingCarbs) || !float.IsFinite(perServingFats) ||
            !float.IsFinite(perServingFiber) || !float.IsFinite(perServingSugarAlcohols) ||
            !float.IsFinite(perServingNetCarbs) || !float.IsFinite(perServingGrams))
            return Calculate([], safeServings);

        return new RecipeNutrientSummary(
            TotalCalories: totalCalories,
            TotalProtein: totalProtein,
            TotalCarbs: totalCarbs,
            TotalFats: totalFats,
            TotalFiber: totalFiber,
            TotalSugarAlcohols: totalSugarAlcohols,
            TotalNetCarbs: totalNetCarbs,
            PerServingCalories: perServingCalories,
            PerServingProtein: perServingProtein,
            PerServingCarbs: perServingCarbs,
            PerServingFats: perServingFats,
            PerServingFiber: perServingFiber,
            PerServingSugarAlcohols: perServingSugarAlcohols,
            PerServingNetCarbs: perServingNetCarbs,
            PerServingGrams: perServingGrams,
            TotalRawWeightGrams: totalRawWeight,
            CookedWeightGrams: effectiveCookedWeight,
            ServingsCount: safeServings
        );
    }
}
