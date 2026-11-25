using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

/// <summary>The overview read.</summary>
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

/// <summary>Assembles the day's overview, including any suggested meals.</summary>
public sealed class NutritionOverviewUseCase(INutritionOverviewStore store) : INutritionOverviewUseCase
{
    /// <summary>Loads the nutrition overview for a day.</summary>
    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await store.GetOverviewAsync(request, cancellationToken).ConfigureAwait(false);
        return response with
        {
            Targets = NutritionTargetContractMapper.WithMealDistribution(
                response.Targets, request.BreakfastPreference)
        };
    }

    /// <summary>The plan's macro targets for the profile.</summary>
    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        var preference = breakfastPreference ?? "Savoury";
        var targets = await store.GetPlanTargetsAsync(preference, cancellationToken).ConfigureAwait(false);
        return NutritionTargetContractMapper.WithMealDistribution(targets, preference);
    }

    /// <summary>Logs a meal from the overview screen.</summary>
    public Task LogMealAsync(
        LogMealRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMealIngredients(request.Ingredients, nameof(request));

        return store.LogMealAsync(request, cancellationToken);
    }

    /// <summary>Updates a meal logged from the overview screen.</summary>
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

    /// <summary>The ingredient inputs of a planned meal.</summary>
    public Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        int mealId,
        CancellationToken cancellationToken = default)
    {
        if (mealId <= 0)
            return Task.FromResult<IReadOnlyList<MealIngredientInputDto>>([]);

        return store.GetMealIngredientsAsync(mealId, cancellationToken);
    }

    /// <summary>Creates a custom food from the overview screen.</summary>
    public Task<CustomFoodResultDto> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CustomFoodCreationRule.IsValidRequest(
                request.Name,
                request.ServingAmount,
                request.ServingUnit,
                request.Calories,
                request.Protein,
                request.Carbs,
                request.Fats,
                request.IronMilligrams,
                request.VitaminB12Micrograms,
                request.VitaminDMicrograms,
                request.CalciumMilligrams))
            throw new ArgumentException("Custom food values are invalid.", nameof(request));

        return store.CreateCustomFoodAsync(request, cancellationToken);
    }

    /// <summary>The meal suggestions for the day.</summary>
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

    /// <summary>The day's meal plan.</summary>
    public Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        return store.GetDayMealPlanAsync(breakfastPreference ?? "Savoury", cancellationToken);
    }

    /// <summary>Regenerates one planned meal.</summary>
    public Task<DayMealPlanItemDto> RefreshDayMealAsync(
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return store.RefreshDayMealAsync(item, breakfastPreference ?? "Savoury", cancellationToken);
    }

    /// <summary>Seeds the reference foods once, so the overview never shows an empty catalog.</summary>
    public Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default)
    {
        return store.EnsureReferenceDataReadyAsync(cancellationToken);
    }

    /// <summary>Refuses a meal with no ingredients or a non-positive weight.</summary>
    private static void ValidateMealIngredients(IReadOnlyList<MealIngredientInputDto>? ingredients, string parameterName)
    {
        if (ingredients is null || !NutritionLogRule.HasValidIngredients(ingredients.Select(item => item.Grams).ToArray()))
            throw new ArgumentException("A meal requires at least one ingredient.", parameterName);
    }
}
