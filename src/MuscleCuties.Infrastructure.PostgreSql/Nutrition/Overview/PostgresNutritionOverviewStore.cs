using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Targets;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;

/// <summary>
/// Owner-scoped PostgreSQL store for nutrition overview, grouped meals, custom foods, daily consumption, and meal planning.
/// </summary>
public sealed class PostgresNutritionOverviewStore(
    PostgresDbContext db,
    IServerNutritionTargetProjectionStore targetStore) : IServerNutritionOverviewStore, INutritionOverviewStore
{
    private const float GeneralDefaultCalories = 2000f;
    private const float GeneralDefaultProtein = 140f;
    private const float GeneralDefaultCarbs = 200f;
    private const float GeneralDefaultFats = 60f;

    private const float BreakfastCalorieRatio = 0.25f;
    private const float LunchCalorieRatio = 0.35f;
    private const float DinnerCalorieRatio = 0.30f;
    private const float SnackCalorieRatio = 0.10f;

    private static readonly (string Group, string Name, string Unit, float Goal)[] StandardMicronutrients =
    [
        ("Fiber", "Fiber", "g", 25f),
        ("Vitamins", "Vitamin A", "mcg", 700f),
        ("Vitamins", "Vitamin C", "mg", 75f),
        ("Vitamins", "Vitamin D", "mcg", 15f),
        ("Vitamins", "Vitamin B6", "mg", 1.3f),
        ("Vitamins", "Vitamin B12", "mcg", 2.4f),
        ("Vitamins", "Folate", "mcg", 400f),
        ("Minerals", "Calcium", "mg", 1000f),
        ("Minerals", "Iron", "mg", 18f),
        ("Minerals", "Magnesium", "mg", 320f),
        ("Minerals", "Zinc", "mg", 8f),
        ("Minerals", "Potassium", "mg", 2600f)
    ];

    private static readonly ReferenceConceptItem[] SavouryBreakfastCatalog =
    [
        new("Scrambled Eggs with Toast", "Two scrambled eggs with whole-grain toast and spinach.",
            380f, 26f, 30f, 16f, ["Eggs", "Whole-Grain Bread", "Spinach", "Olive Oil"]),
        new("Tofu Scramble with Vegetables", "Seasoned tofu scramble with bell peppers, onions, and avocado.",
            370f, 22f, 32f, 17f, ["Tofu", "Bell Peppers", "Onions", "Avocado", "Olive Oil"])
    ];

    private static readonly ReferenceConceptItem[] SweetBreakfastCatalog =
    [
        new("Berry Protein Oatmeal", "Rolled oats with vanilla protein powder, chia seeds, and mixed berries.",
            400f, 32f, 50f, 8f, ["Rolled Oats", "Protein Powder", "Mixed Berries", "Chia Seeds"]),
        new("Greek Yogurt Parfait", "Greek yogurt layered with honey, walnuts, and blueberries.",
            380f, 30f, 44f, 9f, ["Greek Yogurt", "Blueberries", "Walnuts", "Honey"])
    ];

    private static readonly ReferenceConceptItem[] LunchCatalog =
    [
        new("Mediterranean Chicken Rice Bowl", "Grilled chicken breast with brown rice, cucumber, and tzatziki.",
            550f, 45f, 58f, 14f, ["Chicken Breast", "Brown Rice", "Cucumber", "Tzatziki", "Olive Oil"]),
        new("Tuna Quinoa Salad", "Flaked tuna with quinoa, mixed greens, cherry tomatoes, and olive oil.",
            520f, 42f, 50f, 16f, ["Tuna", "Quinoa", "Mixed Greens", "Cherry Tomatoes", "Olive Oil"]),
        new("Chickpea Veggie Power Bowl", "Roasted chickpeas with roasted sweet potato, kale, and tahini drizzle.",
            500f, 20f, 70f, 16f, ["Chickpeas", "Sweet Potato", "Kale", "Tahini"])
    ];

    private static readonly ReferenceConceptItem[] DinnerCatalog =
    [
        new("Baked Salmon with Sweet Potato", "Wild salmon fillet with baked sweet potato and steamed asparagus.",
            600f, 46f, 52f, 22f, ["Salmon Fillet", "Sweet Potato", "Asparagus", "Olive Oil"]),
        new("Lean Turkey Chili", "Ground turkey simmered with black beans, diced tomatoes, and brown rice.",
            560f, 48f, 60f, 14f, ["Ground Turkey", "Black Beans", "Tomatoes", "Brown Rice"]),
        new("Tofu Stir-Fry with Edamame", "Crispy tofu with edamame, broccoli, bell peppers, and jasmine rice.",
            520f, 32f, 64f, 15f, ["Tofu", "Edamame", "Broccoli", "Jasmine Rice", "Sesame Oil"])
    ];

    private static readonly ReferenceConceptItem[] SnackCatalog =
    [
        new("Apple Slices with Peanut Butter", "Crisp apple slices paired with natural creamy peanut butter.",
            220f, 7f, 28f, 12f, ["Apple", "Peanut Butter"]),
        new("Cottage Cheese with Berries", "Low-fat cottage cheese topped with fresh raspberries and blueberries.",
            200f, 24f, 18f, 3f, ["Cottage Cheese", "Raspberries", "Blueberries"]),
        new("Rice Cakes with Avocado", "Brown rice cakes topped with mashed ripe avocado and sea salt.",
            210f, 4f, 26f, 11f, ["Rice Cakes", "Avocado", "Sea Salt"])
    ];

    public static int ToStableLoggedMealIntId(Guid mealId)
    {
        var bytes = mealId.ToByteArray();
        var raw = (BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4)) & 0x7FFFFFFF;
        return raw > 0 ? raw : 1;
    }

    public static int ToStableCustomFoodIntId(Guid customFoodId)
    {
        var bytes = customFoodId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, sizeof(int));
    }

    public async Task<NutritionOverviewResponse> GetOverviewAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<NutritionOverviewResponse>(async () =>
        {
            var target = await targetStore.LoadAsync(userId, date, cancellationToken).ConfigureAwait(false);

            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);

            var snapshots = await db.FoodItemEntries.AsNoTracking()
                .Where(x => x.UserId == userId && x.LocalDate == date && !x.IsDeleted)
                .OrderBy(x => x.LoggedAtTimestamp).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var meals = await db.LoggedMeals.AsNoTracking()
                .Where(x => x.UserId == userId && x.Date == date)
                .OrderBy(x => x.LoggedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var mealIds = meals.Select(x => x.Id).ToArray();

            var catalogEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.CatalogFoodItems.AsNoTracking() on entry.CatalogFoodItemId equals food.Id
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new MealEntryNutrientRow(
                    entry.LoggedMealId,
                    entry.Grams,
                    food.Name,
                    (float)(food.Calories ?? 0),
                    (float)(food.Protein ?? 0),
                    (float)(food.Carbs ?? 0),
                    (float)(food.Fats ?? 0),
                    (float)(food.Fiber ?? 0),
                    (float)(food.VitaminA ?? 0),
                    (float)(food.VitaminC ?? 0),
                    (float)(food.VitaminD ?? 0),
                    (float)(food.VitaminB6 ?? 0),
                    (float)(food.VitaminB12 ?? 0),
                    (float)(food.Folate ?? 0),
                    (float)(food.Calcium ?? 0),
                    (float)(food.Iron ?? 0),
                    (float)(food.Magnesium ?? 0),
                    (float)(food.Zinc ?? 0),
                    (float)(food.Potassium ?? 0)
                )).ToListAsync(cancellationToken).ConfigureAwait(false);

            var userFoodEntries = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.UserFoodItems.AsNoTracking() on new { entry.UserId, entry.UserFoodItemId } equals new { food.UserId, UserFoodItemId = (Guid?)food.Id }
                where entry.UserId == userId && mealIds.Contains(entry.LoggedMealId)
                select new MealEntryNutrientRow(
                    entry.LoggedMealId,
                    entry.Grams,
                    food.Name,
                    (float)(food.Calories ?? 0),
                    (float)(food.Protein ?? 0),
                    (float)(food.Carbs ?? 0),
                    (float)(food.Fats ?? 0),
                    (float)(food.Fiber ?? 0),
                    (float)(food.VitaminA ?? 0),
                    (float)(food.VitaminC ?? 0),
                    (float)(food.VitaminD ?? 0),
                    (float)(food.VitaminB6 ?? 0),
                    (float)(food.VitaminB12 ?? 0),
                    (float)(food.Folate ?? 0),
                    (float)(food.Calcium ?? 0),
                    (float)(food.Iron ?? 0),
                    (float)(food.Magnesium ?? 0),
                    (float)(food.Zinc ?? 0),
                    (float)(food.Potassium ?? 0)
                )).ToListAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var allMealEntries = catalogEntries.Concat(userFoodEntries).ToArray();

            var snapshotCalories = snapshots.Sum(s => (float)s.Calories);
            var snapshotProtein = snapshots.Sum(s => (float)s.Protein);
            var snapshotCarbs = snapshots.Sum(s => (float)s.Carbs);
            var snapshotFat = snapshots.Sum(s => (float)s.Fat);

            var mealCalories = allMealEntries.Sum(e => e.Calories * (float)e.Grams / 100f);
            var mealProtein = allMealEntries.Sum(e => e.Protein * (float)e.Grams / 100f);
            var mealCarbs = allMealEntries.Sum(e => e.Carbs * (float)e.Grams / 100f);
            var mealFat = allMealEntries.Sum(e => e.Fats * (float)e.Grams / 100f);

            var consumed = new NutritionConsumedMacrosDto(
                snapshotCalories + mealCalories,
                snapshotProtein + mealProtein,
                snapshotCarbs + mealCarbs,
                snapshotFat + mealFat);

            var targetMacros = new NutritionTargetMacrosDto(
                target.Calories, target.Protein, target.Carbs, target.Fats);

            var targetBreakdown = new NutritionTargetBreakdownDto(
                target.Source,
                target.StartingEnergyCalories,
                target.GoalAdjustmentCalories,
                target.PhaseAdjustmentCalories,
                target.CalculatedCalories,
                target.SavedRequestedCalories,
                target.Calories,
                target.HasSavedMacroOverride,
                target.SavedRequestDiffersFromShownTarget);

            var mealCards = new List<MealSummaryDto>();

            foreach (var s in snapshots)
            {
                var cal = (float)s.Calories;
                var pro = (float)s.Protein;
                var carb = (float)s.Carbs;
                var fat = (float)s.Fat;
                mealCards.Add(new MealSummaryDto(
                    s.Id,
                    0,
                    s.LoggedAtTimestamp.ToString("h:mm tt", CultureInfo.InvariantCulture),
                    s.MealType,
                    s.Name,
                    $"{cal:N0} kcal",
                    $"{pro:N0}g P · {carb:N0}g C · {fat:N0}g F",
                    string.Empty,
                    string.Empty,
                    cal, pro, carb, fat,
                    BuildMacroBreakdownDtos(cal, pro, carb, fat),
                    []
                ));
            }

            foreach (var meal in meals)
            {
                var items = allMealEntries.Where(e => e.LoggedMealId == meal.Id).ToList();
                var c = items.Sum(i => i.Calories * (float)i.Grams / 100f);
                var p = items.Sum(i => i.Protein * (float)i.Grams / 100f);
                var cb = items.Sum(i => i.Carbs * (float)i.Grams / 100f);
                var f = items.Sum(i => i.Fats * (float)i.Grams / 100f);
                var names = items.Select(i => i.Name).Distinct().Take(3).ToList();
                var name = names.Count == 0 ? "Logged meal" : string.Join(" + ", names);

                mealCards.Add(new MealSummaryDto(
                    meal.Id,
                    ToStableLoggedMealIntId(meal.Id),
                    meal.LoggedAt.ToString("h:mm tt", CultureInfo.InvariantCulture),
                    meal.MealType,
                    name,
                    $"{c:N0} kcal",
                    $"{p:N0}g P · {cb:N0}g C · {f:N0}g F",
                    string.Empty,
                    string.Empty,
                    c, p, cb, f,
                    BuildMacroBreakdownDtos(c, p, cb, f),
                    []
                ));
            }

            var micronutrientDtos = StandardMicronutrients.Select(def =>
            {
                var amount = def.Name switch
                {
                    "Fiber" => allMealEntries.Sum(e => e.Fiber * (float)e.Grams / 100f),
                    "Vitamin A" => allMealEntries.Sum(e => e.VitaminA * (float)e.Grams / 100f),
                    "Vitamin C" => allMealEntries.Sum(e => e.VitaminC * (float)e.Grams / 100f),
                    "Vitamin D" => allMealEntries.Sum(e => e.VitaminD * (float)e.Grams / 100f),
                    "Vitamin B6" => allMealEntries.Sum(e => e.VitaminB6 * (float)e.Grams / 100f),
                    "Vitamin B12" => allMealEntries.Sum(e => e.VitaminB12 * (float)e.Grams / 100f),
                    "Folate" => allMealEntries.Sum(e => e.Folate * (float)e.Grams / 100f),
                    "Calcium" => allMealEntries.Sum(e => e.Calcium * (float)e.Grams / 100f),
                    "Iron" => allMealEntries.Sum(e => e.Iron * (float)e.Grams / 100f),
                    "Magnesium" => allMealEntries.Sum(e => e.Magnesium * (float)e.Grams / 100f),
                    "Zinc" => allMealEntries.Sum(e => e.Zinc * (float)e.Grams / 100f),
                    "Potassium" => allMealEntries.Sum(e => e.Potassium * (float)e.Grams / 100f),
                    _ => 0f
                };
                return new DailyMicronutrientDto(def.Group, def.Name, def.Unit, amount, def.Goal);
            }).ToList();

            var (focusTitle, focusCopy) = GetPhaseFocus(target.Phase);

            return new NutritionOverviewResponse(
                targetMacros,
                consumed,
                target.Phase.ToString(),
                target.Phase.ToString(),
                focusTitle,
                focusCopy,
                mealCards,
                micronutrientDtos,
                breakfastPreference,
                target.AvailabilityCode,
                target.Source,
                targetBreakdown
            );
        }).ConfigureAwait(false);
    }

    public async Task LogMealAsync(
        Guid userId,
        LogMealRequest request,
        CancellationToken cancellationToken = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);

            var nowUtc = DateTime.UtcNow;
            var loggedAtUtc = DateTime.SpecifyKind(
                request.Date.ToDateTime(TimeOnly.FromTimeSpan(request.MealTime)),
                DateTimeKind.Utc);

            var meal = new LoggedMealRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Date = request.Date,
                LoggedAt = loggedAtUtc,
                MealType = request.MealType,
                AcceptedSuggestionName = request.AcceptedSuggestionName,
                AcceptedSuggestionPhase = request.AcceptedSuggestionPhase,
                CreatedAt = nowUtc
            };
            db.LoggedMeals.Add(meal);

            var entries = await ResolveMealEntriesAsync(userId, meal.Id, request.Ingredients, nowUtc, cancellationToken)
                .ConfigureAwait(false);
            db.LoggedMealEntries.AddRange(entries);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task UpdateMealAsync(
        Guid userId,
        UpdateMealRequest request,
        CancellationToken cancellationToken = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);

            var userMeals = await db.LoggedMeals
                .Where(m => m.UserId == userId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var meal = userMeals.FirstOrDefault(m => ToStableLoggedMealIntId(m.Id) == request.MealId)
                       ?? throw new InvalidOperationException("Logged meal not found for the authenticated user.");

            meal.Date = request.Date;
            meal.MealType = request.MealType;
            meal.LoggedAt = DateTime.SpecifyKind(
                request.Date.ToDateTime(TimeOnly.FromTimeSpan(request.MealTime)),
                DateTimeKind.Utc);

            var existingEntries = await db.LoggedMealEntries
                .Where(e => e.UserId == userId && e.LoggedMealId == meal.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            db.LoggedMealEntries.RemoveRange(existingEntries);

            var replacementEntries = await ResolveMealEntriesAsync(
                    userId, meal.Id, request.Ingredients, DateTime.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            db.LoggedMealEntries.AddRange(replacementEntries);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        Guid userId,
        int mealId,
        CancellationToken cancellationToken = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IReadOnlyList<MealIngredientInputDto>>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);

            var userMeals = await db.LoggedMeals.AsNoTracking()
                .Where(m => m.UserId == userId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var meal = userMeals.FirstOrDefault(m => ToStableLoggedMealIntId(m.Id) == mealId);
            if (meal is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return [];
            }

            var catalogRows = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.CatalogFoodItems.AsNoTracking() on entry.CatalogFoodItemId equals food.Id
                where entry.UserId == userId && entry.LoggedMealId == meal.Id
                select new MealIngredientInputDto(
                    food.Id,
                    food.Name,
                    (float)entry.Grams,
                    (float)entry.Grams,
                    "g",
                    (float)(food.Calories ?? 0m),
                    (float)(food.Protein ?? 0m),
                    (float)(food.Carbs ?? 0m),
                    (float)(food.Fats ?? 0m),
                    food.DataType ?? "Catalog"
                )).ToListAsync(cancellationToken).ConfigureAwait(false);

            var customRows = await (from entry in db.LoggedMealEntries.AsNoTracking()
                join food in db.UserFoodItems.AsNoTracking() on new { entry.UserId, entry.UserFoodItemId } equals new { food.UserId, UserFoodItemId = (Guid?)food.Id }
                where entry.UserId == userId && entry.LoggedMealId == meal.Id
                select new
                {
                    Food = food,
                    entry.Grams
                }).ToListAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var customDtos = customRows.Select(row => new MealIngredientInputDto(
                ToStableCustomFoodIntId(row.Food.Id),
                row.Food.Name,
                (float)row.Grams,
                (float)row.Grams,
                "g",
                (float)(row.Food.Calories ?? 0m),
                (float)(row.Food.Protein ?? 0m),
                (float)(row.Food.Carbs ?? 0m),
                (float)(row.Food.Fats ?? 0m),
                "Custom"));

            return catalogRows.Concat(customDtos).ToList();
        }).ConfigureAwait(false);
    }

    public async Task<CustomFoodResultDto> CreateCustomFoodAsync(
        Guid userId,
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CustomFoodCreationRule.TryCreate(
                request.Name,
                request.ServingAmount,
                request.ServingUnit,
                request.Calories,
                request.Protein,
                request.Carbs,
                request.Fats,
                out var validated,
                out var error))
        {
            throw new ArgumentException(error);
        }

        if (!CustomFoodCreationRule.TryNormalizeMicronutrients(
                request.IronMilligrams,
                request.VitaminB12Micrograms,
                request.VitaminDMicrograms,
                request.CalciumMilligrams,
                validated.ServingGrams,
                out var micronutrients))
            throw new ArgumentException("Micronutrients are outside the supported range.", nameof(request));

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<CustomFoodResultDto>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(db, userId, cancellationToken).ConfigureAwait(false);

            var nowUtc = DateTime.UtcNow;
            var record = new UserFoodItemRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Name = validated.Name,
                Calories = (decimal)validated.CaloriesPer100Grams,
                Protein = (decimal)validated.ProteinPer100Grams,
                Carbs = (decimal)validated.CarbsPer100Grams,
                Fats = (decimal)validated.FatsPer100Grams,
                BrandOwner = string.IsNullOrWhiteSpace(request.CompanySource) ? null : request.CompanySource.Trim(),
                Iron = micronutrients.IronMilligramsPer100Grams,
                VitaminB12 = micronutrients.VitaminB12MicrogramsPer100Grams,
                VitaminD = micronutrients.VitaminDMicrogramsPer100Grams,
                Calcium = micronutrients.CalciumMilligramsPer100Grams,
                DataType = "Custom",
                ServingSize = (decimal)validated.ServingAmount,
                ServingSizeUnit = validated.ServingUnit,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            db.UserFoodItems.Add(record);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var numericId = ToStableCustomFoodIntId(record.Id);
            var cals = (float)record.Calories.Value;
            var pro = (float)record.Protein.Value;
            var carbs = (float)record.Carbs.Value;
            var fats = (float)record.Fats.Value;
            return new CustomFoodResultDto(
                numericId,
                record.Name,
                cals,
                pro,
                carbs,
                fats,
                record.BrandOwner ?? "Custom food",
                (float)record.ServingSize.Value,
                record.ServingSizeUnit,
                null,
                $"{cals:N0} kcal · P {pro:N1}g · C {carbs:N1}g · F {fats:N1}g per 100g");
        }).ConfigureAwait(false);
    }

    private async Task<List<LoggedMealEntryRecord>> ResolveMealEntriesAsync(
        Guid userId,
        Guid loggedMealId,
        IReadOnlyList<MealIngredientInputDto> ingredients,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var userCustomFoods = await db.UserFoodItems
            .Where(f => f.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var catalogFoods = await db.CatalogFoodItems.AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<LoggedMealEntryRecord>(ingredients.Count);
        foreach (var ingredient in ingredients)
        {
            var matchedCustom = userCustomFoods.FirstOrDefault(f => ToStableCustomFoodIntId(f.Id) == ingredient.FoodItemId);
            if (matchedCustom is not null)
            {
                entries.Add(new LoggedMealEntryRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    LoggedMealId = loggedMealId,
                    UserFoodItemId = matchedCustom.Id,
                    Grams = (decimal)ingredient.Grams
                });
                continue;
            }

            var matchedCatalog = catalogFoods.FirstOrDefault(f => f.Id == ingredient.FoodItemId)
                                 ?? catalogFoods.FirstOrDefault(f => string.Equals(f.Name, ingredient.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedCatalog is not null)
            {
                entries.Add(new LoggedMealEntryRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    LoggedMealId = loggedMealId,
                    CatalogFoodItemId = matchedCatalog.Id,
                    Grams = (decimal)ingredient.Grams
                });
                continue;
            }

            var matchedCustomByName = userCustomFoods.FirstOrDefault(f =>
                string.Equals(f.Name, ingredient.Name, StringComparison.OrdinalIgnoreCase));
            if (matchedCustomByName is null)
            {
                matchedCustomByName = new UserFoodItemRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    Name = string.IsNullOrWhiteSpace(ingredient.Name) ? "Meal ingredient" : ingredient.Name.Trim(),
                    Calories = (decimal)Math.Max(0f, ingredient.Calories),
                    Protein = (decimal)Math.Max(0f, ingredient.Protein),
                    Carbs = (decimal)Math.Max(0f, ingredient.Carbs),
                    Fats = (decimal)Math.Max(0f, ingredient.Fats),
                    DataType = "Custom",
                    ServingSize = 100m,
                    ServingSizeUnit = "g",
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                db.UserFoodItems.Add(matchedCustomByName);
                userCustomFoods.Add(matchedCustomByName);
            }

            entries.Add(new LoggedMealEntryRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                LoggedMealId = loggedMealId,
                UserFoodItemId = matchedCustomByName.Id,
                Grams = (decimal)ingredient.Grams
            });
        }

        return entries;
    }

    public async Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        Guid userId,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        var target = await targetStore.LoadAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken).ConfigureAwait(false);
        return new NutritionTargetMacrosDto(target.Calories, target.Protein, target.Carbs, target.Fats);
    }

    public async Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        Guid userId,
        DateOnly date,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        var target = await targetStore.LoadAsync(userId, date, cancellationToken).ConfigureAwait(false);
        var totalCalories = target.Calories > 0 ? target.Calories : GeneralDefaultCalories;
        var totalProtein = target.Protein > 0 ? target.Protein : GeneralDefaultProtein;
        var totalCarbs = target.Carbs > 0 ? target.Carbs : GeneralDefaultCarbs;
        var totalFats = target.Fats > 0 ? target.Fats : GeneralDefaultFats;

        var isSweet = string.Equals(breakfastPreference, "Sweet", StringComparison.OrdinalIgnoreCase);

        var breakfastItem = BuildDayMealPlanItem(
            "Breakfast",
            totalCalories * BreakfastCalorieRatio,
            totalProtein * BreakfastCalorieRatio,
            totalCarbs * BreakfastCalorieRatio,
            totalFats * BreakfastCalorieRatio,
            totalCalories,
            isSweet ? SweetBreakfastCatalog[0] : SavouryBreakfastCatalog[0]);

        var lunchItem = BuildDayMealPlanItem(
            "Lunch",
            totalCalories * LunchCalorieRatio,
            totalProtein * LunchCalorieRatio,
            totalCarbs * LunchCalorieRatio,
            totalFats * LunchCalorieRatio,
            totalCalories,
            LunchCatalog[0]);

        var dinnerItem = BuildDayMealPlanItem(
            "Dinner",
            totalCalories * DinnerCalorieRatio,
            totalProtein * DinnerCalorieRatio,
            totalCarbs * DinnerCalorieRatio,
            totalFats * DinnerCalorieRatio,
            totalCalories,
            DinnerCatalog[0]);

        var snackItem = BuildDayMealPlanItem(
            "Snack",
            totalCalories * SnackCalorieRatio,
            totalProtein * SnackCalorieRatio,
            totalCarbs * SnackCalorieRatio,
            totalFats * SnackCalorieRatio,
            totalCalories,
            SnackCatalog[0]);

        return [breakfastItem, lunchItem, dinnerItem, snackItem];
    }

    public Task<DayMealPlanItemDto> RefreshDayMealAsync(
        Guid userId,
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default)
    {
        var catalog = GetCatalogForMealType(item.MealType, breakfastPreference);
        if (catalog.Count == 0)
            return Task.FromResult(item);

        var currentName = item.Suggestion?.ConceptName;
        var candidate = catalog.FirstOrDefault(c => !string.Equals(c.Name, currentName, StringComparison.OrdinalIgnoreCase))
                        ?? catalog[0];

        var refreshed = BuildDayMealPlanItem(
            item.MealType,
            item.TargetCalories,
            item.TargetProtein,
            item.TargetCarbs,
            item.TargetFats,
            item.DailyCaloriesTarget,
            candidate);

        return Task.FromResult(refreshed);
    }

    public Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        Guid userId,
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames = null,
        CancellationToken cancellationToken = default)
    {
        var catalog = GetCatalogForMealType(mealType, breakfastPreference);
        var targetRatio = mealType.ToLowerInvariant() switch
        {
            "breakfast" => BreakfastCalorieRatio,
            "lunch" => LunchCalorieRatio,
            "dinner" => DinnerCalorieRatio,
            "snack" => SnackCalorieRatio,
            _ => LunchCalorieRatio
        };

        var targetCals = GeneralDefaultCalories * targetRatio;

        var suggestions = catalog
            .Where(c => excludeConceptNames == null || !excludeConceptNames.Contains(c.Name))
            .Select(c => MapConceptToSuggestion(c, targetCals))
            .ToList();

        return Task.FromResult<IReadOnlyList<MealSuggestionDto>>(suggestions);
    }

    public Task<NutritionOverviewResponse> GetOverviewAsync(
        NutritionOverviewRequest request,
        CancellationToken cancellationToken = default) =>
        GetOverviewAsync(Guid.Empty, request.Date, request.BreakfastPreference, cancellationToken);

    public Task<NutritionTargetMacrosDto> GetPlanTargetsAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default) =>
        GetPlanTargetsAsync(Guid.Empty, breakfastPreference, cancellationToken);

    public Task LogMealAsync(LogMealRequest request, CancellationToken cancellationToken = default) =>
        LogMealAsync(Guid.Empty, request, cancellationToken);

    public Task UpdateMealAsync(UpdateMealRequest request, CancellationToken cancellationToken = default) =>
        UpdateMealAsync(Guid.Empty, request, cancellationToken);

    public Task<IReadOnlyList<MealIngredientInputDto>> GetMealIngredientsAsync(
        int mealId,
        CancellationToken cancellationToken = default) =>
        GetMealIngredientsAsync(Guid.Empty, mealId, cancellationToken);

    public Task<CustomFoodResultDto> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        CancellationToken cancellationToken = default) =>
        CreateCustomFoodAsync(Guid.Empty, request, cancellationToken);

    public Task<IReadOnlyList<MealSuggestionDto>> GetMealSuggestionsAsync(
        string mealType,
        string breakfastPreference,
        IReadOnlySet<string>? excludeConceptNames,
        CancellationToken cancellationToken = default) =>
        GetMealSuggestionsAsync(Guid.Empty, mealType, breakfastPreference, excludeConceptNames, cancellationToken);

    public Task<IReadOnlyList<DayMealPlanItemDto>> GetDayMealPlanAsync(
        string breakfastPreference,
        CancellationToken cancellationToken = default) =>
        GetDayMealPlanAsync(Guid.Empty, DateOnly.FromDateTime(DateTime.UtcNow), breakfastPreference, cancellationToken);

    public Task<DayMealPlanItemDto> RefreshDayMealAsync(
        DayMealPlanItemDto item,
        string breakfastPreference,
        CancellationToken cancellationToken = default) =>
        RefreshDayMealAsync(Guid.Empty, item, breakfastPreference, cancellationToken);

    public Task EnsureReferenceDataReadyAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static IReadOnlyList<ReferenceConceptItem> GetCatalogForMealType(string mealType, string breakfastPreference)
    {
        var isSweet = string.Equals(breakfastPreference, "Sweet", StringComparison.OrdinalIgnoreCase);
        return mealType.ToLowerInvariant() switch
        {
            "breakfast" => isSweet ? SweetBreakfastCatalog : SavouryBreakfastCatalog,
            "lunch" => LunchCatalog,
            "dinner" => DinnerCatalog,
            "snack" => SnackCatalog,
            _ => LunchCatalog
        };
    }

    private static DayMealPlanItemDto BuildDayMealPlanItem(
        string mealType,
        float targetCalories,
        float targetProtein,
        float targetCarbs,
        float targetFats,
        float dailyTargetCalories,
        ReferenceConceptItem concept)
    {
        var suggestion = MapConceptToSuggestion(concept, targetCalories);
        return new DayMealPlanItemDto(
            mealType,
            targetCalories,
            targetProtein,
            targetCarbs,
            targetFats,
            dailyTargetCalories,
            false,
            suggestion,
            string.Empty
        );
    }

    private static MealSuggestionDto MapConceptToSuggestion(ReferenceConceptItem concept, float targetCalories)
    {
        var baseCals = concept.BaseCalories > 0 ? concept.BaseCalories : 1f;
        var scale = targetCalories / baseCals;
        var scaledCalories = targetCalories;
        var scaledProtein = concept.BaseProtein * scale;
        var scaledCarbs = concept.BaseCarbs * scale;
        var scaledFats = concept.BaseFats * scale;

        var components = concept.Ingredients.Select((ing, idx) =>
            new MealSuggestionComponentDto(
                idx + 1,
                ing,
                "Ingredient",
                "Base",
                100f * scale,
                false,
                scaledCalories / concept.Ingredients.Count,
                scaledProtein / concept.Ingredients.Count,
                scaledCarbs / concept.Ingredients.Count,
                scaledFats / concept.Ingredients.Count
            )).ToList();

        var group = new MealSuggestionComponentGroupDto("Ingredients", "Food24", components);

        return new MealSuggestionDto(
            concept.Name,
            concept.Description,
            [],
            components,
            [group],
            scaledCalories,
            scaledProtein,
            scaledCarbs,
            scaledFats,
            1.0f,
            "Balanced",
            "Food24",
            concept.Description
        );
    }

    private static IReadOnlyList<MacroBreakdownItemDto> BuildMacroBreakdownDtos(float cals, float p, float c, float f)
    {
        var totalCals = cals <= 0 ? 1f : cals;
        var pCals = p * 4f;
        var cCals = c * 4f;
        var fCals = f * 9f;

        return
        [
            new MacroBreakdownItemDto("Protein", p, pCals, Math.Clamp(pCals / totalCals, 0f, 1f)),
            new MacroBreakdownItemDto("Carbs", c, cCals, Math.Clamp(cCals / totalCals, 0f, 1f)),
            new MacroBreakdownItemDto("Fats", f, fCals, Math.Clamp(fCals / totalCals, 0f, 1f))
        ];
    }

    private static (string title, string copy) GetPhaseFocus(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => ("Iron + comfort", "Iron, warm carbs, easy protein."),
        CyclePhase.Follicular => ("Fuel base", "Clean carbs, lean protein."),
        CyclePhase.Ovulatory => ("Peak plate", "Hydrate, balance, push."),
        CyclePhase.Luteal => ("Steady cravings", "Fiber, magnesium, steady carbs."),
        _ => ("Nutrition focus", "Balanced nutrition guidance.")
    };

    private sealed record MealEntryNutrientRow(
        Guid LoggedMealId,
        decimal Grams,
        string Name,
        float Calories,
        float Protein,
        float Carbs,
        float Fats,
        float Fiber,
        float VitaminA,
        float VitaminC,
        float VitaminD,
        float VitaminB6,
        float VitaminB12,
        float Folate,
        float Calcium,
        float Iron,
        float Magnesium,
        float Zinc,
        float Potassium);

    private sealed record ReferenceConceptItem(
        string Name,
        string Description,
        float BaseCalories,
        float BaseProtein,
        float BaseCarbs,
        float BaseFats,
        IReadOnlyList<string> Ingredients);
}
