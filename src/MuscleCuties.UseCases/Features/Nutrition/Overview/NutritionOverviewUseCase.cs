using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

public interface INutritionOverviewUseCase
{
    Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default);

    Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task LogMealAsync(
        LogMealRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMealAsync(
        UpdateMealRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        int mealId,
        CancellationToken cancellationToken = default);

    Task<CustomFoodResultDto> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task<DayMealPlanItemDto> RefreshDayMealAsync(
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default);
}

public sealed class NutritionOverviewUseCase(INutritionOverviewStore store) : INutritionOverviewUseCase
{
    public Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.GetOverviewAsync(request, cancellationToken);
    }

    public Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        return store.GetPlanTargetsAsync(breakfastPreference ?? "Savoury", cancellationToken);
    }

    public Task LogMealAsync(
        LogMealRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMealIngredients(request.Ingredients, nameof(request));

        return store.LogMealAsync(request, cancellationToken);
    }

    public Task UpdateMealAsync(
        UpdateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MealId <= 0)
            throw new ArgumentException("Invalid meal ID.", nameof(request));
        ValidateMealIngredients(request.Ingredients, nameof(request));

        return store.UpdateMealAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        int mealId,
        CancellationToken cancellationToken = default)
    {
        if (mealId <= 0)
            return Task.FromResult<IReadOnlyList<MealIngredientInputDto>>([]);

        return store.GetMealIngredientsAsync(mealId, cancellationToken);
    }

    public Task<CustomFoodResultDto> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Custom food name is required.", nameof(request));
        if (request.ServingAmount <= 0 || !float.IsFinite(request.ServingAmount))
            throw new ArgumentException("Serving amount must be positive and finite.", nameof(request));
        if (request.Calories < 0 || !float.IsFinite(request.Calories) ||
            request.Protein < 0 || !float.IsFinite(request.Protein) ||
            request.Carbs < 0 || !float.IsFinite(request.Carbs) ||
            request.Fats < 0 || !float.IsFinite(request.Fats))
            throw new ArgumentException("Nutrition metrics must be non-negative finite numbers.", nameof(request));
        if (new[] { request.IronMilligrams, request.VitaminB12Micrograms,
                request.VitaminDMicrograms, request.CalciumMilligrams }
            .Any(value => value is < 0 || value.HasValue && !float.IsFinite(value.Value)))
            throw new ArgumentException("Micronutrients must be non-negative finite numbers.", nameof(request));

        return store.CreateCustomFoodAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames,
        CancellationToken cancellationToken = default)
    {
        return store.GetMealSuggestionsAsync(
            mealType ?? "Lunch",
            breakfastPreference ?? "Savoury",
            excludeConceptNames,
            cancellationToken);
    }

    public Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        return store.GetDayMealPlanAsync(breakfastPreference ?? "Savoury", cancellationToken);
    }

    public Task<DayMealPlanItemDto> RefreshDayMealAsync(
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return store.RefreshDayMealAsync(item, breakfastPreference ?? "Savoury", cancellationToken);
    }

    public Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default)
    {
        return store.EnsureReferenceDataReadyAsync(cancellationToken);
    }

    private static void ValidateMealIngredients(IReadOnlyList<MealIngredientInputDto>? ingredients, string parameterName)
    {
        if (ingredients is null || !MealIngredientWeightRule.HasIngredients(ingredients.Count))
            throw new ArgumentException("A meal requires at least one ingredient.", parameterName);

        foreach (var ingredient in ingredients)
            if (!MealIngredientWeightRule.IsValidGrams(ingredient.Grams))
                throw new ArgumentException("Ingredient grams must be positive and finite.", parameterName);
    }
}
