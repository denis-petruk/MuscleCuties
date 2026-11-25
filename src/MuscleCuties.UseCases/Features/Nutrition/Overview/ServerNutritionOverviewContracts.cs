using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

/// <summary>The server-side overview read.</summary>
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

/// <summary>Assembles the overview on the server from one consistent read.</summary>
public sealed class ServerNutritionOverviewUseCase(IServerNutritionOverviewStore store)
{
    /// <summary>Loads the server's nutrition overview for a user and day.</summary>
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

    /// <summary>The plan's macro targets for a user.</summary>
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

    /// <summary>The day's meal plan for a user.</summary>
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

    /// <summary>Regenerates one planned meal for a user.</summary>
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

    /// <summary>The meal suggestions for a user's day.</summary>
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

    /// <summary>Logs a meal for a user.</summary>
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

    /// <summary>Updates a user's logged meal.</summary>
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

    /// <summary>The ingredient inputs of a user's planned meal.</summary>
    public Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        Guid userId,
        int mealId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || mealId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mealId));
        return store.GetMealIngredientsAsync(userId, mealId, cancellationToken);
    }

    /// <summary>Creates a custom food for a user.</summary>
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
