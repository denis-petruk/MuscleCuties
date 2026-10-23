using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Inputs;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Infrastructure.Models.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.Infrastructure.Nutrition.Overview;

public sealed class LocalNutritionOverviewStore(
    IServiceScopeFactory scopeFactory,
    IReferenceDataPreparationService referenceDataPreparation) : INutritionOverviewStore
{
    private const float GeneralStartingCalories = 2000f;
    private const float GeneralStartingProteinGrams = 140f;
    private const float GeneralStartingCarbsGrams = 200f;
    private const float GeneralStartingFatsGrams = 60f;
    private const float NoLoggedIntake = 0f;

    private static readonly IReadOnlyList<MicronutrientDefinition> MicronutrientDefinitions =
    [
        new("Fiber", "Fiber", "g", 25f, food => food.Fiber),
        new("Vitamins", "Vitamin A", "mcg", 700f, food => food.VitaminA, true),
        new("Vitamins", "Vitamin C", "mg", 75f, food => food.VitaminC),
        new("Vitamins", "Vitamin D", "mcg", 15f, food => food.VitaminD, true),
        new("Vitamins", "Vitamin B6", "mg", 1.3f, food => food.VitaminB6),
        new("Vitamins", "Vitamin B12", "mcg", 2.4f, food => food.VitaminB12),
        new("Vitamins", "Folate", "mcg", 400f, food => food.Folate),
        new("Minerals", "Calcium", "mg", 1000f, food => food.Calcium),
        new("Minerals", "Iron", "mg", 18f, food => food.Iron),
        new("Minerals", "Magnesium", "mg", 320f, food => food.Magnesium),
        new("Minerals", "Zinc", "mg", 8f, food => food.Zinc),
        new("Minerals", "Potassium", "mg", 2600f, food => food.Potassium)
    ];

    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var date = request.Date;
        var preference = ParsePreference(request.BreakfastPreference);

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var authService = sp.GetRequiredService<IAuthService>();
            var userId = await authService.GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
            {
                var unavailable = NutritionPhaseAvailabilityRule.Resolve(false, CyclePhase.Unknown);
                return new NutritionOverviewResponse(
                    new NutritionTargetMacrosDto(GeneralStartingCalories, GeneralStartingProteinGrams,
                        GeneralStartingCarbsGrams, GeneralStartingFatsGrams),
                    new NutritionConsumedMacrosDto(NoLoggedIntake, NoLoggedIntake, NoLoggedIntake, NoLoggedIntake),
                    unavailable.Phase.ToString(),
                    unavailable.Phase.ToString(),
                    "Complete profile setup",
                    "Set up your profile to see personal nutrition targets.",
                    [],
                    [],
                    preference.ToString(),
                    unavailable.Availability.ToString(),
                    "General starting targets",
                    MapTargetBreakdown(NutritionTargetProvenance.GeneralStarting(GeneralStartingCalories))
                );
            }

            var cycleService = sp.GetRequiredService<ICycleService>();
            var phase = await cycleService.GetCurrentPhaseAsync(userId).ConfigureAwait(false);
            var phaseState = NutritionPhaseAvailabilityRule.Resolve(true, phase);
            phase = phaseState.Phase;
            var observedPhase = phase;

            var nutritionService = sp.GetRequiredService<INutritionService>();
            var targetTask =
                nutritionService.GetDailyPlanAsync(userId, phase, date.ToDateTime(TimeOnly.MinValue), preference);
            var dateTime = date.ToDateTime(TimeOnly.MinValue);
            var mealsTask = nutritionService.GetLoggedMealsByDateAsync(userId, dateTime);
            var consumedTask = nutritionService.GetConsumedTotalsAsync(userId, dateTime);
            var trackingService = sp.GetRequiredService<INutritionTrackingService>();
            var trackingLogTask = trackingService.GetLogForDateAsync(date);

            await Task.WhenAll(targetTask, mealsTask, consumedTask, trackingLogTask).ConfigureAwait(false);

            var plan = await targetTask.ConfigureAwait(false);
            // The planner can suppress phase-based nutrition targets for the current
            // reproductive context even when cycle tracking has a phase estimate.
            phaseState = NutritionPhaseAvailabilityRule.ResolveForTargets(
                true, observedPhase, plan?.Phase ?? phase);
            phase = phaseState.Phase;
            NutritionTargetMacrosDto targetMacros;
            ProfileNutritionGoals goals;

            if (plan is not null)
            {
                targetMacros = new NutritionTargetMacrosDto(plan.Calories, plan.Protein, plan.Carbs, plan.Fats);
                goals = plan.Goals;
            }
            else
            {
                var (cal, pro, carb, fat) =
                    await nutritionService.CalculateDailyTargetsAsync(userId, phase).ConfigureAwait(false);
                targetMacros = new NutritionTargetMacrosDto(cal, pro, carb, fat);
                goals = ProfileNutritionGoals.FromCalculated(cal, pro, carb, fat, 25f, 2.3f);
            }

            var rawMeals = (await mealsTask.ConfigureAwait(false)).ToList();
            var consumed = await consumedTask.ConfigureAwait(false);
            var trackingLog = await trackingLogTask.ConfigureAwait(false);

            var legacyCards = rawMeals.ToDictionary(
                m => CanonicalMealIdentity.ForLoggedMeal(m.Id),
                m => BuildMealSummaryDto(m, rawMeals, goals));

            var mealDtos = trackingLog.Entries.Select(entry =>
            {
                if (legacyCards.TryGetValue(entry.Id, out var existing))
                    return existing;

                var macros = new MacroNutrients((float)entry.Calories, (float)entry.Protein, (float)entry.Carbs,
                    (float)entry.Fat);
                return new MealSummaryDto(
                    entry.Id,
                    0,
                    entry.LoggedAtTimestamp.ToString("h:mm tt", CultureInfo.CurrentCulture),
                    entry.MealType.ToString(),
                    entry.Name,
                    $"{entry.Calories:N0} kcal",
                    macros.ToMacroText(),
                    string.Empty,
                    string.Empty,
                    (float)entry.Calories,
                    (float)entry.Protein,
                    (float)entry.Carbs,
                    (float)entry.Fat,
                    BuildMacroBreakdownDtos(macros),
                    []
                );
            }).ToList();

            var micronutrientDtos = BuildMicronutrients(rawMeals, goals)
                .Select(m => new DailyMicronutrientDto(m.Group, m.Name, m.Unit, m.Amount, m.Goal))
                .ToList();

            var consumedMacros =
                new NutritionConsumedMacrosDto(consumed.Calories, consumed.Protein, consumed.Carbs, consumed.Fats);
            var targetProvenance = plan?.TargetProvenance ??
                                   NutritionTargetProvenance.Unavailable(targetMacros.Calories);

            var (focusTitle, focusCopy) = phaseState.Availability switch
            {
                NutritionPhaseAvailability.Available => GetPhaseFocus(phase),
                NutritionPhaseAvailability.TargetGuidanceUnavailable =>
                    ("Nutrition guidance", "Phase-based nutrition guidance is unavailable with current settings."),
                _ => ("Cycle phase unavailable", "Log a cycle to see phase-based suggestions.")
            };

            return new NutritionOverviewResponse(
                targetMacros,
                consumedMacros,
                phase.ToString(),
                phase.ToString(),
                focusTitle,
                focusCopy,
                mealDtos,
                micronutrientDtos,
                preference.ToString(),
                phaseState.Availability.ToString(),
                TargetSourceLabel(targetProvenance.Source),
                MapTargetBreakdown(targetProvenance)
            );
        }).ConfigureAwait(false);
    }

    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pref = ParsePreference(breakfastPreference);

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return new NutritionTargetMacrosDto(GeneralStartingCalories, GeneralStartingProteinGrams,
                    GeneralStartingCarbsGrams, GeneralStartingFatsGrams);

            var phase = await sp.GetRequiredService<ICycleService>().GetCurrentPhaseAsync(userId).ConfigureAwait(false);
            var nutritionService = sp.GetRequiredService<INutritionService>();
            var plan = await nutritionService.GetDailyPlanAsync(userId, phase, DateTime.Today, pref)
                .ConfigureAwait(false);

            if (plan is not null)
                return new NutritionTargetMacrosDto(plan.Calories, plan.Protein, plan.Carbs, plan.Fats);

            var (cals, pro, carb, fat) =
                await nutritionService.CalculateDailyTargetsAsync(userId, phase).ConfigureAwait(false);
            return new NutritionTargetMacrosDto(cals, pro, carb, fat);
        }).ConfigureAwait(false);
    }

    public async Task LogMealAsync(
        LogMealRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mealType = Enum.TryParse<MealType>(request.MealType, true, out var mt) ? mt : MealType.Snack;
        CyclePhase? phase = Enum.TryParse<CyclePhase>(request.AcceptedSuggestionPhase, true, out var cp) ? cp : null;
        var loggedAt = request.Date.ToDateTime(TimeOnly.FromTimeSpan(request.MealTime));

        var inputs = request.Ingredients
            .Select(i => new MealIngredientInput(i.FoodItemId, i.Grams))
            .ToList();

        await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                throw new UnauthorizedAccessException("Must be signed in to log a meal.");

            await sp.GetRequiredService<INutritionService>().LogMealAsync(
                userId,
                inputs,
                mealType,
                loggedAt,
                request.AcceptedSuggestionName,
                phase).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task UpdateMealAsync(
        UpdateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mealType = Enum.TryParse<MealType>(request.MealType, true, out var mt) ? mt : MealType.Snack;
        var loggedAt = request.Date.ToDateTime(TimeOnly.FromTimeSpan(request.MealTime));

        var inputs = request.Ingredients
            .Select(i => new MealIngredientInput(i.FoodItemId, i.Grams))
            .ToList();

        await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                throw new UnauthorizedAccessException("Must be signed in to update a meal.");

            await sp.GetRequiredService<INutritionService>().UpdateMealAsync(
                userId,
                request.MealId,
                inputs,
                mealType,
                loggedAt).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        int mealId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return [];

            var meal = await sp.GetRequiredService<INutritionService>().GetLoggedMealAsync(userId, mealId)
                .ConfigureAwait(false);
            if (meal is null)
                return [];

            return meal.Entries
                .Where(e => e.FoodItem is not null)
                .Select(e => new MealIngredientInputDto(
                    e.FoodItemId,
                    e.FoodItem!.Name,
                    e.Grams,
                    e.Grams,
                    "g",
                    e.FoodItem.Calories,
                    e.FoodItem.Protein,
                    e.FoodItem.Carbs,
                    e.FoodItem.Fats,
                    BuildSourceSummary(e.FoodItem)))
                .ToList();
        }).ConfigureAwait(false);
    }

    public async Task<CustomFoodResultDto> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var nutritionService = sp.GetRequiredService<INutritionService>();
            var food = await nutritionService.CreateCustomFoodAsync(new CustomFoodInput(
                request.Name,
                request.ServingAmount,
                request.ServingUnit,
                request.Calories,
                request.Protein,
                request.Carbs,
                request.Fats)).ConfigureAwait(false);

            return new CustomFoodResultDto(
                food.Id,
                food.Name,
                food.Calories,
                food.Protein,
                food.Carbs,
                food.Fats,
                BuildSourceSummary(food),
                food.ServingSize,
                food.ServingSizeUnit,
                food.ServingOptionsJson,
                BuildNutritionSummary(food));
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mt = Enum.TryParse<MealType>(mealType, true, out var parsedMt) ? parsedMt : MealType.Lunch;
        var pref = ParsePreference(breakfastPreference);

        await referenceDataPreparation.EnsureNutritionReadyAsync().ConfigureAwait(false);

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return [];

            var phase = await sp.GetRequiredService<ICycleService>().GetCurrentPhaseAsync(userId).ConfigureAwait(false);
            var nutritionService = sp.GetRequiredService<INutritionService>();

            var meals = await nutritionService.GetSuggestedMealsAsync(
                userId, mt, pref, phase, DateTime.Today, excludeConceptNames).ConfigureAwait(false);

            return meals.Select(MapSuggestionToDto).ToList();
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pref = ParsePreference(breakfastPreference);

        await referenceDataPreparation.EnsureNutritionReadyAsync().ConfigureAwait(false);

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return [];

            var phase = await sp.GetRequiredService<ICycleService>().GetCurrentPhaseAsync(userId).ConfigureAwait(false);
            var nutritionService = sp.GetRequiredService<INutritionService>();

            var dayPlan = await nutritionService.GetSuggestedMealPlanAsync(userId, pref, phase, DateTime.Today)
                .ConfigureAwait(false);

            return dayPlan.Meals.Select(p => new DayMealPlanItemDto(
                p.MealType.ToString(),
                p.Target.Calories,
                p.Target.Protein,
                p.Target.Carbs,
                p.Target.Fats,
                dayPlan.DailyTargetCalories,
                p.IsAlreadyLogged,
                p.Suggestion is not null ? MapSuggestionToDto(p.Suggestion) : null,
                string.Empty
            )).ToList();
        }).ConfigureAwait(false);
    }

    public async Task<DayMealPlanItemDto> RefreshDayMealAsync(
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pref = ParsePreference(breakfastPreference);
        var mt = Enum.TryParse<MealType>(item.MealType, true, out var parsedMt) ? parsedMt : MealType.Lunch;

        await referenceDataPreparation.EnsureNutritionReadyAsync().ConfigureAwait(false);

        return await ScopedAsync(scopeFactory, async sp =>
        {
            var userId = await sp.GetRequiredService<IAuthService>().GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId <= 0)
                return item;

            var phase = await sp.GetRequiredService<ICycleService>().GetCurrentPhaseAsync(userId).ConfigureAwait(false);
            var nutritionService = sp.GetRequiredService<INutritionService>();

            var target = new MealNutritionTarget(mt, item.TargetCalories, item.TargetProtein, item.TargetCarbs,
                item.TargetFats);
            var suggestions = await nutritionService.GetSuggestedMealsForTargetAsync(
                userId,
                target,
                pref,
                phase,
                DateTime.Today).ConfigureAwait(false);

            var suggestion = suggestions.FirstOrDefault();
            return new DayMealPlanItemDto(
                item.MealType,
                item.TargetCalories,
                item.TargetProtein,
                item.TargetCarbs,
                item.TargetFats,
                item.DailyCaloriesTarget,
                item.IsAlreadyLogged,
                suggestion is not null ? MapSuggestionToDto(suggestion) : null,
                suggestions.Count == 0 ? "Try refreshing again." : string.Empty
            );
        }).ConfigureAwait(false);
    }

    public Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default)
    {
        return referenceDataPreparation.EnsureNutritionReadyAsync();
    }

    private static async Task<T> ScopedAsync<T>(IServiceScopeFactory factory, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.CreateAsyncScope();
        return await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    private static async Task ScopedAsync(IServiceScopeFactory factory, Func<IServiceProvider, Task> action)
    {
        await using var scope = factory.CreateAsyncScope();
        await action(scope.ServiceProvider).ConfigureAwait(false);
    }

    private static MealSuggestionDto MapSuggestionToDto(SuggestedMeal s)
    {
        var components = s.AllComponents
            .SelectMany(component => component.Ingredients.Select(ingredient =>
                new MealSuggestionComponentDto(
                    ingredient.Food.Id,
                    ingredient.Food.Name,
                    MapSlotToComponentType(component.Type).ToString(),
                    MapSlotToRole(component.Type).ToString(),
                    ingredient.Grams,
                    false,
                    ingredient.Food.Calories,
                    ingredient.Food.Protein,
                    ingredient.Food.Carbs,
                    ingredient.Food.Fats
                )))
            .ToList();

        var orderedGroups = new[]
        {
            ("Carb", "Carb base", "Food24"),
            ("Protein", "Protein", "FoodEgg24"),
            ("Vegetable", "Salad / Vegetables", "LeafThree24"),
            ("Sauce", "Sauce", "Drop24")
        };

        var groups = new List<MealSuggestionComponentGroupDto>();
        foreach (var (typeStr, label, icon) in orderedGroups)
        {
            var matching = components.Where(c => c.ComponentType == typeStr).ToList();
            if (matching.Count > 0) groups.Add(new MealSuggestionComponentGroupDto(label, icon, matching));
        }

        var directionsText = BuildDirections(s, components);

        return new MealSuggestionDto(
            s.ConceptName,
            s.ConceptDescription,
            s.SpiceBlends.Select(b => b.Name).ToList(),
            components,
            groups,
            s.Macros.Calories,
            s.Macros.Protein,
            s.Macros.Carbs,
            s.Macros.Fats,
            s.Score,
            s.Style.ToString(),
            ResolveIconGlyph(s.ConceptName),
            directionsText
        );
    }

    private static ComponentType MapSlotToComponentType(MealConceptSlotType slotType)
    {
        return slotType switch
        {
            MealConceptSlotType.CarbBase => ComponentType.Carb,
            MealConceptSlotType.ProteinBase => ComponentType.Protein,
            MealConceptSlotType.VitaminBase => ComponentType.Vegetable,
            MealConceptSlotType.Sauce => ComponentType.Sauce,
            _ => ComponentType.Mixed
        };
    }

    private static ComponentRole MapSlotToRole(MealConceptSlotType slotType)
    {
        return slotType switch
        {
            MealConceptSlotType.CarbBase => ComponentRole.CarbBase,
            MealConceptSlotType.ProteinBase => ComponentRole.ProteinBase,
            MealConceptSlotType.VitaminBase => ComponentRole.SideSalad,
            MealConceptSlotType.Sauce => ComponentRole.SauceOnSide,
            _ => ComponentRole.CarbBase
        };
    }

    private static string ResolveIconGlyph(string conceptName)
    {
        return conceptName switch
        {
            "Berry Oat Bowl" => "BowlSalad24",
            "Yogurt Parfait" => "FoodCake24",
            "PB Banana Oats" => "BowlChopsticks24",
            "Shakshuka" => "FoodEgg24",
            "Avocado Toast" => "FoodToast24",
            "Breakfast Taco" => "FoodPizza24",
            "Breakfast Wrap" => "FoodChickenLeg24",
            "Breakfast Burger" => "Food24",
            "Egg and Potato Plate" => "FoodCarrot24",
            "Loaded Grain Bowl" => "FoodGrains24",
            "Protein Wrap" => "FoodToast24",
            "Bean Power Plate" => "LeafThree24",
            "Taco Bowl" => "BowlSalad24",
            "Loaded Potato" => "FoodCarrot24",
            "Quinoa Salad Plate" => "LeafTwo24",
            "Protein Sweet Potato Plate" => "FoodChickenLeg24",
            "Bean Stew Bowl" => "BowlChopsticks24",
            "Rice Protein Bowl" => "FoodGrains24",
            "Stuffed Potato" => "Food24",
            "Lentil Power Bowl" => "LeafOne24",
            "Mini Sandwich" => "FoodToast24",
            "Protein Snack Plate" => "FoodFish24",
            "Yogurt Bowl" => "FoodCake24",
            _ => "BowlSalad24"
        };
    }

    private static string BuildDirections(SuggestedMeal s, IReadOnlyList<MealSuggestionComponentDto> components)
    {
        var carb = components.FirstOrDefault(c => c.ComponentType == "Carb")?.Name ?? "carb base";
        var protein = components.FirstOrDefault(c => c.ComponentType == "Protein")?.Name ?? "protein";
        var veg = components.FirstOrDefault(c => c.ComponentType == "Vegetable")?.Name ?? "vegetables";
        var sauce = components.FirstOrDefault(c => c.ComponentType == "Sauce")?.Name;
        var spice = s.SpiceBlends.Count > 0 ? s.SpiceBlends[0].Name : null;

        var steps = s.Style switch
        {
            MealStyle.Bowl => BuildBowlDirections(carb, protein, veg, sauce, spice),
            MealStyle.Complex => BuildComplexDirections(carb, protein, veg, sauce, spice),
            MealStyle.Handheld => BuildHandheldDirections(carb, protein, veg, sauce, spice),
            MealStyle.Wrapped => BuildWrappedDirections(carb, protein, veg, sauce, spice),
            _ => BuildPlatedDirections(carb, protein, veg, sauce, spice)
        };

        return string.Join("\n", steps.Select((step, i) => $"{i + 1}. {step}"));
    }

    private static List<string> BuildBowlDirections(string carb, string protein, string veg, string? sauce,
        string? spice)
    {
        var steps = new List<string>
        {
            $"Cook {carb} and place in a bowl as the base.",
            $"Prepare {protein} and arrange on top.",
            $"Add {veg} fresh or lightly tossed."
        };
        if (sauce is not null) steps.Add($"Drizzle {sauce} over everything.");
        if (spice is not null) steps.Add($"Season with {spice} to finish.");
        steps.Add("Toss gently or eat layered.");
        return steps;
    }

    private static List<string> BuildComplexDirections(string carb, string protein, string veg, string? sauce,
        string? spice)
    {
        var steps = StartProteinSteps(protein, spice, "Brown");
        steps.Add($"Stir in {veg} and cook until tender.");
        if (sauce is not null) steps.Add($"Pour in {sauce} and simmer to coat.");
        steps.Add($"Serve hot over prepared {carb}.");
        return steps;
    }

    private static List<string> BuildHandheldDirections(string carb, string protein, string veg, string? sauce,
        string? spice)
    {
        var steps = StartProteinSteps(protein, spice, "Cook");
        steps.Add($"Lightly toast or warm {carb}.");
        steps.Add($"Layer {protein} onto {carb}.");
        steps.Add($"Top with {veg}.");
        if (sauce is not null) steps.Add($"Finish with a spread of {sauce}.");
        steps.Add("Close or stack and enjoy.");
        return steps;
    }

    private static List<string> BuildWrappedDirections(string carb, string protein, string veg, string? sauce,
        string? spice)
    {
        var steps = StartProteinSteps(protein, spice, "Cook");
        steps.Add($"Warm {carb} until pliable.");
        if (sauce is not null) steps.Add($"Spread {sauce} down the centre of {carb}.");
        steps.Add($"Add {protein} and {veg}.");
        steps.Add("Fold or roll tightly and serve.");
        return steps;
    }

    private static List<string> BuildPlatedDirections(string carb, string protein, string veg, string? sauce,
        string? spice)
    {
        var steps = new List<string>
        {
            $"Cook {carb} and serve on one side of the plate.",
            $"Prepare {protein} and place alongside."
        };
        if (spice is not null) steps.Add($"Season {protein} with {spice} before or after cooking.");
        steps.Add($"Arrange {veg} on the side as a fresh salad or steamed.");
        if (sauce is not null) steps.Add($"Serve {sauce} on the side or drizzle lightly.");
        return steps;
    }

    private static List<string> StartProteinSteps(string protein, string? spice, string cookStep)
    {
        var steps = new List<string>();
        if (spice is not null) steps.Add($"Season {protein} with {spice}.");
        steps.Add($"{cookStep} {protein} until done.");
        return steps;
    }

    private static MealSummaryDto BuildMealSummaryDto(
        LoggedMeal meal,
        IReadOnlyCollection<LoggedMeal> dailyMeals,
        ProfileNutritionGoals goals)
    {
        var entries = meal.Entries.Where(e => e.FoodItem is not null).ToList();
        var macros = MacroNutrientsFoodExtensions.SumMealEntries(entries);
        var micronutrients = BuildMicronutrients([meal], dailyMeals, goals)
            .Select(m => new DailyMicronutrientDto(m.Group, m.Name, m.Unit, m.Amount, m.Goal))
            .ToList();

        return new MealSummaryDto(
            CanonicalMealIdentity.ForLoggedMeal(meal.Id),
            meal.Id,
            meal.LoggedAt.ToString("h:mm tt", CultureInfo.CurrentCulture),
            meal.MealType.ToString(),
            BuildMealCardName(entries),
            $"{macros.Calories:N0} kcal",
            macros.ToMacroText(),
            BuildFiberText(micronutrients),
            BuildMicronutrientSummaryText(micronutrients),
            macros.Calories,
            macros.Protein,
            macros.Carbs,
            macros.Fats,
            BuildMacroBreakdownDtos(macros),
            micronutrients
        );
    }

    private static IReadOnlyList<MacroBreakdownItemDto> BuildMacroBreakdownDtos(MacroNutrients macros)
    {
        var totalCals = macros.Calories <= 0 ? 1f : macros.Calories;
        var pCals = macros.Protein * 4f;
        var cCals = macros.Carbs * 4f;
        var fCals = macros.Fats * 9f;

        return
        [
            new MacroBreakdownItemDto("Protein", macros.Protein, pCals, Math.Clamp(pCals / totalCals, 0f, 1f)),
            new MacroBreakdownItemDto("Carbs", macros.Carbs, cCals, Math.Clamp(cCals / totalCals, 0f, 1f)),
            new MacroBreakdownItemDto("Fats", macros.Fats, fCals, Math.Clamp(fCals / totalCals, 0f, 1f))
        ];
    }

    private static string BuildFiberText(IReadOnlyList<DailyMicronutrientDto> micronutrients)
    {
        var fiber = micronutrients.FirstOrDefault(m =>
            string.Equals(m.Name, "Fiber", StringComparison.OrdinalIgnoreCase));
        return fiber is null || fiber.Amount <= 0f ? string.Empty : $"{fiber.Amount:N1}g fiber";
    }

    private static string BuildMicronutrientSummaryText(IReadOnlyList<DailyMicronutrientDto> micronutrients)
    {
        var notable = micronutrients
            .Where(m => !string.Equals(m.Name, "Fiber", StringComparison.OrdinalIgnoreCase) && m.Goal > 0f &&
                        m.Amount >= m.Goal * 0.2f)
            .Take(3)
            .Select(m => m.Name)
            .ToList();

        return notable.Count == 0 ? "No micronutrients tracked yet" : string.Join(" · ", notable);
    }

    private static string BuildMealCardName(IReadOnlyList<LoggedMealEntry> entries)
    {
        var names = entries
            .Select(entry => ShortenIngredientName(entry.FoodItem!.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count switch
        {
            0 => "Logged meal",
            1 => names[0],
            2 => $"{names[0]} + {names[1]}",
            _ => $"{names[0]} + {names[1]} + {names.Count - 2} more"
        };
    }

    private static string ShortenIngredientName(string name)
    {
        var cleanName = name
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? name.Trim();

        return cleanName.Length <= 24 ? cleanName : $"{cleanName[..21]}...";
    }

    private static (string title, string copy) GetPhaseFocus(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => ("Iron + comfort", "Iron, warm carbs, easy protein."),
            CyclePhase.Follicular => ("Fuel base", "Clean carbs, lean protein."),
            CyclePhase.Ovulatory => ("Peak plate", "Hydrate, balance, push."),
            CyclePhase.Luteal => ("Steady cravings", "Fiber, magnesium, steady carbs."),
            _ => ("Nutrition focus", string.Empty)
        };
    }

    private static BreakfastPreference ParsePreference(string? preference)
    {
        return Enum.TryParse<BreakfastPreference>(preference, true, out var p) ? p : BreakfastPreference.Savoury;
    }

    private static string TargetSourceLabel(NutritionTargetSource source) => source switch
    {
        NutritionTargetSource.GeneralStarting => "General starting targets",
        NutritionTargetSource.Calculated => "Calculated targets",
        NutritionTargetSource.SavedOverride => "Your saved targets",
        _ => "Target source unavailable"
    };

    private static NutritionTargetBreakdownDto MapTargetBreakdown(NutritionTargetProvenance provenance) => new(
        provenance.Source.ToString(),
        provenance.StartingEnergyCalories,
        provenance.GoalAdjustmentCalories,
        provenance.PhaseAdjustmentCalories,
        provenance.CalculatedCalories,
        provenance.SavedRequestedCalories,
        provenance.ShownCalories,
        provenance.HasSavedMacroOverride,
        provenance.SavedRequestDiffersFromShownTarget);

    private static string BuildSourceSummary(FoodItem food)
    {
        return string.IsNullOrWhiteSpace(food.DataType) ? (food.FdcId.HasValue ? "USDA" : "Local") : food.DataType;
    }

    private static string BuildNutritionSummary(FoodItem food)
    {
        var options = FoodServingOptions.BuildOptionsForFood(food);
        var opt = options.FirstOrDefault();
        return opt is null ? $"{food.Calories:N0} kcal / 100g" : $"{opt.Label}: {food.Calories:N0} kcal";
    }

    private static IEnumerable<MicroItem> BuildMicronutrients(
        IEnumerable<LoggedMeal> meals,
        ProfileNutritionGoals goals)
    {
        return BuildMicronutrients(meals, meals, goals);
    }

    private static IEnumerable<MicroItem> BuildMicronutrients(
        IEnumerable<LoggedMeal> sourceMeals,
        IEnumerable<LoggedMeal> dailyMeals,
        ProfileNutritionGoals goals)
    {
        var sourceMealList = sourceMeals.ToList();
        var dailyMealList = dailyMeals.ToList();
        var entryList = sourceMealList
            .SelectMany(meal => meal.Entries)
            .Where(entry => entry.FoodItem is not null)
            .ToList();

        foreach (var definition in MicronutrientDefinitions)
        {
            var amount = definition.RequiresDietaryFat
                ? FatSolubleVitaminAbsorption.SumAbsorbableNutrient(
                    sourceMealList,
                    dailyMealList,
                    definition.ValueSelector)
                : entryList.Sum(entry =>
                    definition.ValueSelector(entry.FoodItem!) * entry.Grams / 100f);

            yield return new MicroItem(
                definition.Group,
                definition.Name,
                definition.Unit,
                amount,
                definition.GoalSelector(goals)
            );
        }
    }

    private sealed record MicronutrientDefinition(
        string Group,
        string Name,
        string Unit,
        float Goal,
        Func<FoodItem, float> ValueSelector,
        bool RequiresDietaryFat = false)
    {
        public float GoalSelector(ProfileNutritionGoals goals)
        {
            return Name switch
            {
                "Fiber" => UseGoal(goals.Fiber, Goal),
                "Vitamin A" => UseGoal(goals.VitaminA, Goal),
                "Vitamin C" => UseGoal(goals.VitaminC, Goal),
                "Vitamin D" => UseGoal(goals.VitaminD, Goal),
                "Vitamin B6" => UseGoal(goals.VitaminB6, Goal),
                "Vitamin B12" => UseGoal(goals.VitaminB12, Goal),
                "Folate" => UseGoal(goals.Folate, Goal),
                "Calcium" => UseGoal(goals.Calcium, Goal),
                "Iron" => UseGoal(goals.Iron, Goal),
                "Magnesium" => UseGoal(goals.Magnesium, Goal),
                "Zinc" => UseGoal(goals.Zinc, Goal),
                "Potassium" => UseGoal(goals.Potassium, Goal),
                _ => Goal
            };
        }

        private static float UseGoal(float? customGoal, float fallback)
        {
            return customGoal is > 0f ? customGoal.Value : fallback;
        }
    }

    private sealed record MicroItem(string Group, string Name, string Unit, float Amount, float Goal);
}
