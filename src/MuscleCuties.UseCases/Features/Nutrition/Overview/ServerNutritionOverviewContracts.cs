using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

public interface IServerNutritionOverviewStore
{
    Task<NutritionOverviewResponse> GetOverviewAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        Guid userId,
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task<DayMealPlanItemDto> RefreshDayMealAsync(
        Guid userId,
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        Guid userId,
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames = null,
        CancellationToken cancellationToken = default);

    Task LogMealAsync(
        Guid userId,
        LogMealRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMealAsync(
        Guid userId,
        UpdateMealRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        Guid userId,
        int mealId,
        CancellationToken cancellationToken = default);

    Task<CustomFoodResultDto> CreateCustomFoodAsync(
        Guid userId,
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ServerNutritionOverviewUseCase(IServerNutritionOverviewStore store)
{
    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference = "Savoury",
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !NutritionLogRule.IsSupportedDate(date))
            throw new ArgumentOutOfRangeException(nameof(date));
        var response = await store.GetOverviewAsync(userId, date, breakfastPreference, cancellationToken)
            .ConfigureAwait(false);
        return response with
        {
            Targets = NutritionTargetContractMapper.WithMealDistribution(response.Targets, breakfastPreference)
        };
    }

    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        Guid userId,
        string breakfastPreference = "Savoury",
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId));
        var targets = await store.GetPlanTargetsAsync(userId, breakfastPreference, cancellationToken)
            .ConfigureAwait(false);
        return NutritionTargetContractMapper.WithMealDistribution(targets, breakfastPreference);
    }

    public Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference = "Savoury",
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !NutritionLogRule.IsSupportedDate(date))
            throw new ArgumentOutOfRangeException(nameof(date));
        return store.GetDayMealPlanAsync(userId, date, breakfastPreference, cancellationToken);
    }

    public Task<DayMealPlanItemDto> RefreshDayMealAsync(
        Guid userId,
        DayMealPlanItemDto item,
        string breakfastPreference = "Savoury",
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || item is null)
            throw new ArgumentOutOfRangeException(nameof(item));
        return store.RefreshDayMealAsync(userId, item, breakfastPreference, cancellationToken);
    }

    public Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        Guid userId,
        string mealType,
        string breakfastPreference = "Savoury",
        IReadOnlySet<string>? excludeConceptNames = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(mealType))
            throw new ArgumentOutOfRangeException(nameof(mealType));
        return store.GetMealSuggestionsAsync(userId, mealType, breakfastPreference, excludeConceptNames, cancellationToken);
    }

    public Task LogMealAsync(
        Guid userId,
        LogMealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId));
        ArgumentNullException.ThrowIfNull(request);
        if (!NutritionLogRule.IsSupportedDate(request.Date) ||
            !NutritionLogRule.IsValidMeal(request.MealType, request.Ingredients?.Select(i => i.Grams).ToArray()))
            throw new ArgumentException("Meal request contains invalid date, meal type, or ingredient amounts.", nameof(request));
        return store.LogMealAsync(userId, request, cancellationToken);
    }

    public Task UpdateMealAsync(
        Guid userId,
        UpdateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.MealId <= 0 ||
            !NutritionLogRule.IsSupportedDate(request.Date) ||
            !NutritionLogRule.IsValidMeal(request.MealType, request.Ingredients?.Select(i => i.Grams).ToArray()))
            throw new ArgumentException("Update meal request contains invalid meal ID, date, or ingredient amounts.", nameof(request));
        return store.UpdateMealAsync(userId, request, cancellationToken);
    }

    public Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        Guid userId,
        int mealId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || mealId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mealId));
        return store.GetMealIngredientsAsync(userId, mealId, cancellationToken);
    }

    public Task<CustomFoodResultDto> CreateCustomFoodAsync(
        Guid userId,
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId));
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
            throw new ArgumentException("Custom food request contains invalid values.", nameof(request));
        return store.CreateCustomFoodAsync(userId, request, cancellationToken);
    }
}
