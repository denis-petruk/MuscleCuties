using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

public interface INutritionOverviewStore
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
