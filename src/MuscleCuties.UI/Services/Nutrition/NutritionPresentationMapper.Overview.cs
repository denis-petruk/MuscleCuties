using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using CyclePhaseDto = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using DomainCyclePhase = MuscleCuties.Core.Models.Enums.Cycle.CyclePhase;

namespace MuscleCuties.UI.Services.Nutrition;

public static partial class NutritionPresentationMapper
{
    public static FoodPortionCatalog FromPortionCatalog(FoodPortionCatalogOutputDto item) =>
        new(item.GramUnit, item.SupportedUnits,
            item.StandardOptions.Select(o => new FoodServingOption(o.Label, o.Unit, o.Grams)).ToList());

    public static FoodPortionCatalog GetPortionCatalog(IRecipePortionUseCase useCase) =>
        FromPortionCatalog(useCase.GetCatalog());

    public static NutritionMealDistribution? FromMealDistribution(NutritionMealDistributionDto? item) =>
        item is null ? null : new(item.Breakfast, item.Lunch, item.Dinner, item.Snack);

    public static NutritionTargetBreakdown? FromTargetBreakdown(NutritionTargetBreakdownDto? item) =>
        item is null
            ? null
            : new NutritionTargetBreakdown(item.Source, item.StartingEnergyCalories, item.GoalAdjustmentCalories,
                item.PhaseAdjustmentCalories, item.CalculatedCalories, item.SavedRequestedCalories,
                item.ShownCalories, item.HasSavedMacroOverride, item.SavedRequestDiffersFromShownTarget);

    public static NutritionMealSummary FromMealSummary(MealSummaryDto item) =>
        new(item.HistoryId, item.LoggedMealId, item.Time, item.MealType, item.Name, item.CaloriesText,
            item.MacrosText, item.FiberText, item.NutrientSummaryText, item.Calories, item.Protein,
            item.Carbs, item.Fats,
            item.MacroItems.Select(m => new MacroBreakdownItem
            {
                Name = m.Name, Grams = m.Grams, Calories = m.Calories, Progress = m.Progress
            }).ToList(),
            item.Micronutrients.Select(FromMicronutrient).ToList(), item.IngredientSummary);

    public static DailyMicronutrient FromMicronutrient(DailyMicronutrientDto item) =>
        new(item.Group, item.Name, item.Unit, item.Amount, item.Goal);

    public static DailyMicronutrientItem FromMicronutrientItem(DailyMicronutrientDto item) =>
        new()
        {
            Group = item.Group,
            Name = item.Name,
            Unit = item.Unit,
            Amount = item.Amount,
            Goal = item.Goal
        };

    public static MicronutrientCoverage? FromMicronutrientCoverage(MicronutrientCoverageDto? item) =>
        item is null
            ? null
            : new MicronutrientCoverage(item.Status, item.CoveredItemCount, item.UncoveredItemCount,
                item.UnavailableItemNames);

    public static CustomFoodResult FromCustomFood(CustomFoodResultDto item) =>
        new(item.FoodItemId, item.Name, item.Calories, item.Protein, item.Carbs, item.Fats,
            item.SourceSummary, item.ServingSize, item.ServingSizeUnit, item.ServingOptionsJson,
            item.NutritionSummary);

    public static NutritionSuggestionComponent FromSuggestionComponent(MealSuggestionComponentDto item) =>
        new(item.FoodItemId, item.Name, item.ComponentType, item.Role, item.Grams, item.IsLocked,
            item.Calories, item.Protein, item.Carbs, item.Fats);

    public static NutritionSuggestionComponentGroup FromSuggestionGroup(MealSuggestionComponentGroupDto item) =>
        new(item.ComponentType, item.Title, item.Items.Select(FromSuggestionComponent).ToList());

    public static NutritionMealSuggestion FromSuggestion(MealSuggestionDto item) =>
        new(item.ConceptName, item.ConceptDescription, item.SpiceBlendNames,
            item.Components.Select(FromSuggestionComponent).ToList(),
            item.ComponentGroups.Select(FromSuggestionGroup).ToList(),
            item.Calories, item.Protein, item.Carbs, item.Fats, item.Score, item.Style, item.IconGlyph,
            item.DirectionsText);

    public static NutritionDayMealPlan FromDayMealPlan(DayMealPlanItemDto item) =>
        new(item.MealType, item.TargetCalories, item.TargetProtein, item.TargetCarbs, item.TargetFats,
            item.DailyCaloriesTarget, item.IsAlreadyLogged,
            item.Suggestion is null ? null : FromSuggestion(item.Suggestion), item.ErrorMessage);

    public static DayMealPlanItemDto ToDayMealPlanRequest(DayMealPlanItem item) =>
        new(item.MealType.ToString(), item.Target.Calories, item.Target.Protein, item.Target.Carbs,
            item.Target.Fats, item.DailyCaloriesTarget, item.IsAlreadyLogged, null);

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

    public static async Task<NutritionDayMealPlan> RefreshDayMealAsync(
        INutritionOverviewUseCase useCase,
        NutritionDayMealPlan item,
        string breakfastPreference) =>
        FromDayMealPlan(await useCase.RefreshDayMealAsync(ToDayMealPlan(item), breakfastPreference));

    public static IReadOnlyList<MealIngredientInputDto> ToMealIngredients(
        IEnumerable<NutritionMealIngredient> items) =>
        items.Select(i => new MealIngredientInputDto(i.FoodItemId, i.Name, i.Grams, i.Amount, i.ServingLabel,
            i.Calories, i.Protein, i.Carbs, i.Fats, i.SourceSummary)).ToList();

    public static NutritionMealIngredient ToMealIngredient(MealIngredientInputDto item) =>
        new(item.FoodItemId, item.Name, item.Grams, item.Amount, item.ServingLabel, item.Calories,
            item.Protein, item.Carbs, item.Fats, item.SourceSummary);

    public static CyclePhaseDto ToPresentationCyclePhase(DomainCyclePhase phase) => phase switch
    {
        DomainCyclePhase.Menstrual => CyclePhaseDto.Menstrual,
        DomainCyclePhase.Follicular => CyclePhaseDto.Follicular,
        DomainCyclePhase.Ovulatory => CyclePhaseDto.Ovulatory,
        DomainCyclePhase.Luteal => CyclePhaseDto.Luteal,
        _ => CyclePhaseDto.Unknown
    };
}
