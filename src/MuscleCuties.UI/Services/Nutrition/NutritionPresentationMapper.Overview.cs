using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using CyclePhaseDto = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using DomainCyclePhase = MuscleCuties.Core.Models.Enums.Cycle.CyclePhase;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>Conversions for the nutrition overview, including portion catalogs and phase-aware targets.</summary>
public static partial class NutritionPresentationMapper
{
    /// <summary>The portion catalog as its UI model.</summary>
    public static FoodPortionCatalog FromPortionCatalog(FoodPortionCatalogOutputDto item) =>
        new(item.GramUnit, item.SupportedUnits,
            item.StandardOptions.Select(o => new FoodServingOption(o.Label, o.Unit, o.Grams)).ToList());

    /// <summary>The portion catalog from the portion use case.</summary>
    public static FoodPortionCatalog GetPortionCatalog(IRecipePortionUseCase useCase) =>
        FromPortionCatalog(useCase.GetCatalog());

    /// <summary>The meal distribution as its UI model.</summary>
    public static NutritionMealDistribution? FromMealDistribution(NutritionMealDistributionDto? item) =>
        item is null ? null : new(item.Breakfast, item.Lunch, item.Dinner, item.Snack);

    /// <summary>The target breakdown as its UI model.</summary>
    public static NutritionTargetBreakdown? FromTargetBreakdown(NutritionTargetBreakdownDto? item) =>
        item is null
            ? null
            : new NutritionTargetBreakdown(item.Source, item.StartingEnergyCalories, item.GoalAdjustmentCalories,
                item.PhaseAdjustmentCalories, item.CalculatedCalories, item.SavedRequestedCalories,
                item.ShownCalories, item.HasSavedMacroOverride, item.SavedRequestDiffersFromShownTarget);

    /// <summary>The meal summary as its UI model.</summary>
    public static NutritionMealSummary FromMealSummary(MealSummaryDto item) =>
        new(item.HistoryId, item.LoggedMealId, item.Time, item.MealType, item.Name, item.CaloriesText,
            item.MacrosText, item.FiberText, item.NutrientSummaryText, item.Calories, item.Protein,
            item.Carbs, item.Fats,
            item.MacroItems.Select(m => new MacroBreakdownItem
            {
                Name = m.Name,
                Grams = m.Grams,
                Calories = m.Calories,
                Progress = m.Progress
            }).ToList(),
            item.Micronutrients.Select(FromMicronutrient).ToList(), item.IngredientSummary);

    /// <summary>The micronutrient as its UI model.</summary>
    public static DailyMicronutrient FromMicronutrient(DailyMicronutrientDto item) =>
        new(item.Group, item.Name, item.Unit, item.Amount, item.Goal);

    /// <summary>The micronutrient as a breakdown item.</summary>
    public static DailyMicronutrientItem FromMicronutrientItem(DailyMicronutrientDto item) =>
        new()
        {
            Group = item.Group,
            Name = item.Name,
            Unit = item.Unit,
            Amount = item.Amount,
            Goal = item.Goal
        };

    /// <summary>The coverage as its UI model.</summary>
    public static MicronutrientCoverage? FromMicronutrientCoverage(MicronutrientCoverageDto? item) =>
        item is null
            ? null
            : new MicronutrientCoverage(item.Status, item.CoveredItemCount, item.UncoveredItemCount,
                item.UnavailableItemNames);

    /// <summary>The custom food result as its UI model.</summary>
    public static CustomFoodResult FromCustomFood(CustomFoodResultDto item) =>
        new(item.FoodItemId, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats,
            item.SourceSummary, item.ServingSize, item.ServingSizeUnit, item.ServingOptionsJson,
            item.NutritionSummary);

    /// <summary>The suggestion component as its UI model.</summary>
    public static NutritionSuggestionComponent FromSuggestionComponent(MealSuggestionComponentDto item) =>
        new(item.FoodItemId, item.Name, item.ComponentType, item.Role, item.Grams, item.IsLocked,
            item.Calories, item.Protein, item.Carbs, item.Fats);

    /// <summary>The component group as its UI model.</summary>
    public static NutritionSuggestionComponentGroup FromSuggestionGroup(MealSuggestionComponentGroupDto item) =>
        new(item.ComponentType, item.Title, item.Items.Select(FromSuggestionComponent).ToList());

    /// <summary>The meal suggestion as its UI model.</summary>
    public static NutritionMealSuggestion FromSuggestion(MealSuggestionDto item) =>
        new(item.ConceptName, item.ConceptDescription, item.SpiceBlendNames,
            item.Components.Select(FromSuggestionComponent).ToList(),
            item.ComponentGroups.Select(FromSuggestionGroup).ToList(),
            item.Calories, item.Protein, item.Carbs, item.Fats, item.Score, item.Style, item.IconGlyph,
            item.DirectionsText);

    /// <summary>The plan item as its UI model.</summary>
    public static NutritionDayMealPlan FromDayMealPlan(DayMealPlanItemDto item) =>
        new(item.MealType, item.TargetCalories, item.TargetProtein, item.TargetCarbs, item.TargetFats,
            item.DailyCaloriesTarget, item.IsAlreadyLogged,
            item.Suggestion is null ? null : FromSuggestion(item.Suggestion), item.ErrorMessage);

    /// <summary>The plan item as a request payload.</summary>
    public static DayMealPlanItemDto ToDayMealPlanRequest(DayMealPlanItem item) =>
        new(item.MealType.ToString(), item.Target.Calories, item.Target.Protein, item.Target.Carbs,
            item.Target.Fats, item.DailyCaloriesTarget, item.IsAlreadyLogged, null);

    /// <summary>The plan item as its payload.</summary>
    public static DayMealPlanItemDto ToDayMealPlan(NutritionDayMealPlan item) =>
        new(item.MealType, item.TargetCalories, item.TargetProtein, item.TargetCarbs, item.TargetFats,
            item.DailyCaloriesTarget, item.IsAlreadyLogged,
            item.Suggestion is null ? null : new MealSuggestionDto(item.Suggestion.ConceptName,
                item.Suggestion.ConceptDescription, item.Suggestion.SpiceBlendNames,
                item.Suggestion.Components.Select(c => new MealSuggestionComponentDto(c.FoodItemId, c.Name,
                    c.ComponentType, c.Role, c.Grams, c.IsLocked, c.Calories, c.Protein, c.Carbs, c.Fats)).ToList(),
                item.Suggestion.ComponentGroups.Select(g => new MealSuggestionComponentGroupDto(g.ComponentType,
                    g.Title, g.Items.Select(c => new MealSuggestionComponentDto(c.FoodItemId, c.Name,
                        c.ComponentType, c.Role, c.Grams, c.IsLocked, c.Calories, c.Protein, c.Carbs, c.Fats)).ToList())).ToList(),
                item.Suggestion.Calories, item.Suggestion.Protein, item.Suggestion.Carbs, item.Suggestion.Fats,
                item.Suggestion.Score, item.Suggestion.Style, item.Suggestion.IconGlyph, item.Suggestion.DirectionsText),
            item.ErrorMessage);

    /// <summary>Regenerates the planned meal through the use case.</summary>
    public static async Task<NutritionDayMealPlan> RefreshDayMealAsync(
        INutritionOverviewUseCase useCase,
        NutritionDayMealPlan item,
        string breakfastPreference) =>
        FromDayMealPlan(await useCase.RefreshDayMealAsync(ToDayMealPlan(item), breakfastPreference));

    /// <summary>The meal ingredients as request payloads.</summary>
    public static IReadOnlyList<MealIngredientInputDto> ToMealIngredients(
        IEnumerable<NutritionMealIngredient> items) =>
        items.Select(i => new MealIngredientInputDto(i.FoodItemId, i.Name, i.Grams, i.Amount, i.ServingLabel,
            i.Calories, i.Protein, i.Carbs, i.Fats, i.SourceSummary)).ToList();

    /// <summary>The payload as a meal ingredient.</summary>
    public static NutritionMealIngredient ToMealIngredient(MealIngredientInputDto item) =>
        new(item.FoodItemId, item.Name, item.Grams, item.Amount, item.ServingLabel, item.Calories,
            item.Protein, item.Carbs, item.Fats, item.SourceSummary);

    /// <summary>The domain phase as the contract phase.</summary>
    public static CyclePhaseDto ToPresentationCyclePhase(DomainCyclePhase phase) => phase switch
    {
        DomainCyclePhase.Menstrual => CyclePhaseDto.Menstrual,
        DomainCyclePhase.Follicular => CyclePhaseDto.Follicular,
        DomainCyclePhase.Ovulatory => CyclePhaseDto.Ovulatory,
        DomainCyclePhase.Luteal => CyclePhaseDto.Luteal,
        _ => CyclePhaseDto.Unknown
    };
}
