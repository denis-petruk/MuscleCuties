using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private async Task SeedSystemMealTemplatesAsync()
    {
        var now = DateTime.UtcNow;
        var definitions = BuildSystemMealTemplateDefinitions();
        var templates = await MealTemplates
            .Where(template => template.IsSystem)
            .ToListAsync();
        var changed = false;

        foreach (var definition in definitions)
        {
            var template = templates.FirstOrDefault(existing =>
                string.Equals(existing.Name, definition.Name, StringComparison.OrdinalIgnoreCase));

            if (template is null)
            {
                template = new MealTemplate
                {
                    Name = definition.Name,
                    CreatedAt = now,
                    IsSystem = true
                };
                templates.Add(template);
                await MealTemplates.AddAsync(template);
                changed = true;
            }

            changed |= ApplyTemplateDefinition(template, definition);
        }

        if (changed)
            await SaveChangesAsync();

        await SeedSystemMealTemplateEntriesAsync(definitions);
    }

    private async Task SeedSystemMealTemplateEntriesAsync(
        IReadOnlyCollection<SystemMealTemplateDefinition> definitions)
    {
        var recipes = definitions.ToDictionary(
            definition => definition.Name,
            definition => definition.Entries,
            StringComparer.OrdinalIgnoreCase);
        var recipeFoodNames = recipes.Values
            .SelectMany(recipe => recipe.Select(entry => entry.FoodName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var templateNames = recipes.Keys.ToList();

        var templates = await MealTemplates
            .Include(template => template.Entries)
            .Where(template => template.IsSystem && templateNames.Contains(template.Name))
            .ToListAsync();

        var foodList = await FoodItems
            .Where(food => recipeFoodNames.Contains(food.Name))
            .ToListAsync();
        var foods = foodList.ToDictionary(food => food.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var template in templates)
        {
            if (!recipes.TryGetValue(template.Name, out var recipe))
                continue;

            if (TemplateEntriesMatch(template.Entries, recipe, foods))
                continue;

            MealTemplateEntries.RemoveRange(template.Entries);
            template.Entries.Clear();

            foreach (var entry in recipe)
            {
                if (!foods.TryGetValue(entry.FoodName, out var food))
                    continue;

                template.Entries.Add(new MealTemplateEntry
                {
                    FoodItemId = food.Id,
                    Grams = entry.Grams
                });
            }

            changed = true;
        }

        if (changed)
            await SaveChangesAsync();
    }

    private static bool ApplyTemplateDefinition(
        MealTemplate template,
        SystemMealTemplateDefinition definition)
    {
        var changed = false;
        changed |= SetIfChanged(template.Description, definition.Description, value => template.Description = value);
        changed |= SetIfChanged(template.DietaryTags, TagsToString(definition.DietaryTags), value => template.DietaryTags = value);
        changed |= SetIfChanged(template.PhaseTags, TagsToString(definition.PhaseTags), value => template.PhaseTags = value);
        changed |= SetIfChanged(template.SortOrder, definition.SortOrder, value => template.SortOrder = value);

        if (template.MealType != definition.MealType)
        {
            template.MealType = definition.MealType;
            changed = true;
        }

        if (!template.IsSystem)
        {
            template.IsSystem = true;
            changed = true;
        }

        return changed;
    }

    private static bool SetIfChanged<T>(T? current, T next, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
            return false;

        apply(next);
        return true;
    }

    private static bool TemplateEntriesMatch(
        ICollection<MealTemplateEntry> currentEntries,
        IReadOnlyCollection<SystemMealTemplateRecipeEntry> recipe,
        IReadOnlyDictionary<string, FoodItem> foods)
    {
        if (currentEntries.Count != recipe.Count)
            return false;

        var currentByFoodId = currentEntries
            .GroupBy(entry => entry.FoodItemId)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Grams));

        foreach (var recipeEntry in recipe)
        {
            if (!foods.TryGetValue(recipeEntry.FoodName, out var food))
                return false;

            if (!currentByFoodId.TryGetValue(food.Id, out var grams))
                return false;

            if (Math.Abs(grams - recipeEntry.Grams) > 0.1f)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<SystemMealTemplateDefinition> BuildSystemMealTemplateDefinitions() =>
    [
        Template(
            "Berry Cottage Power Bowl",
            "Cottage cheese, yogurt, berries, oats, and pumpkin seeds for protein, calcium, magnesium, and steady carbs.",
            MealType.Breakfast,
            10,
            [DietaryTag.Vegetarian, DietaryTag.GlutenFree],
            [CyclePhase.Follicular, CyclePhase.Ovulatory, CyclePhase.Luteal],
            [
                new("Cottage cheese, low-fat", 180f),
                new("Greek yogurt, plain nonfat", 120f),
                new("Certified gluten-free rolled oats, dry", 35f),
                new("Blueberries, raw", 90f),
                new("Pumpkin seeds", 18f)
            ]),
        Template(
            "Vegan Berry Mineral Bowl",
            "Fortified soy milk, oats, blueberries, chia, and nutritional yeast for plant protein, B vitamins, calcium, and fiber.",
            MealType.Breakfast,
            20,
            [DietaryTag.Vegan, DietaryTag.Vegetarian, DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Follicular, CyclePhase.Luteal],
            [
                new("Fortified soy milk, unsweetened", 240f),
                new("Certified gluten-free rolled oats, dry", 45f),
                new("Blueberries, raw", 100f),
                new("Chia seeds", 20f),
                new("Nutritional yeast, fortified", 8f)
            ]),
        Template(
            "Egg Spinach Sweet Potato Plate",
            "Eggs, spinach, sweet potato, and olive oil for vitamin A, protein, iron support, and absorbable fat.",
            MealType.Breakfast,
            30,
            [DietaryTag.Vegetarian, DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Follicular],
            [
                new("Eggs, whole, raw", 100f),
                new("Sweet potato, baked", 170f),
                new("Spinach, raw", 70f),
                new("Olive oil", 7f)
            ]),
        Template(
            "Dense Bean Glow Salad",
            "Chickpeas, black beans, quinoa, spinach, red pepper, avocado, and olive oil for fiber, folate, vitamin C, and potassium.",
            MealType.Lunch,
            40,
            [DietaryTag.Vegan, DietaryTag.Vegetarian, DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Follicular, CyclePhase.Luteal],
            [
                new("Chickpeas, cooked", 100f),
                new("Black beans, cooked", 100f),
                new("Quinoa, cooked", 120f),
                new("Spinach, raw", 60f),
                new("Red bell pepper, raw", 100f),
                new("Avocado, raw", 70f),
                new("Olive oil", 8f)
            ]),
        Template(
            "Chicken Dense Bean Salad",
            "Chicken, chickpeas, quinoa, red pepper, kale, and olive oil for protein, vitamin C, iron, and training carbs.",
            MealType.Lunch,
            50,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Follicular, CyclePhase.Ovulatory],
            [
                new("Chicken breast, raw", 130f),
                new("Chickpeas, cooked", 80f),
                new("Quinoa, cooked", 110f),
                new("Red bell pepper, raw", 100f),
                new("Kale, raw", 60f),
                new("Olive oil", 8f)
            ]),
        Template(
            "Chicken Avocado Quinoa Bowl",
            "Chicken, quinoa, avocado, red pepper, spinach, and olive oil for a regular high-protein training bowl.",
            MealType.Lunch,
            52,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Follicular, CyclePhase.Ovulatory],
            [
                new("Chicken breast, raw", 150f),
                new("Quinoa, cooked", 130f),
                new("Avocado, raw", 70f),
                new("Red bell pepper, raw", 90f),
                new("Spinach, raw", 50f),
                new("Olive oil", 6f)
            ]),
        Template(
            "Tuna Chickpea Power Plate",
            "Tuna, chickpeas, red pepper, kale, avocado, and olive oil for B12, iron, vitamin C, fiber, and steady fats.",
            MealType.Lunch,
            54,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Ovulatory, CyclePhase.Luteal],
            [
                new("Tuna, canned in water", 120f),
                new("Chickpeas, cooked", 120f),
                new("Red bell pepper, raw", 90f),
                new("Kale, raw", 70f),
                new("Avocado, raw", 60f),
                new("Olive oil", 5f)
            ]),
        Template(
            "Margherita Pizza Beans",
            "White beans baked with marinara, spinach, mozzarella, parmesan, and olive oil.",
            MealType.Dinner,
            60,
            [DietaryTag.Vegetarian],
            [CyclePhase.Menstrual, CyclePhase.Luteal],
            [
                new("Cannellini beans, cooked", 220f),
                new("Marinara sauce", 150f),
                new("Spinach, raw", 70f),
                new("Mozzarella cheese, part-skim", 60f),
                new("Parmesan cheese", 10f),
                new("Olive oil", 8f)
            ]),
        Template(
            "Vegan Pizza Beans",
            "White beans, marinara, kale, tofu, nutritional yeast, and olive oil for a dairy-free pizza-beans bowl.",
            MealType.Dinner,
            70,
            [DietaryTag.Vegan, DietaryTag.Vegetarian, DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Luteal],
            [
                new("Cannellini beans, cooked", 240f),
                new("Marinara sauce", 160f),
                new("Kale, raw", 70f),
                new("Firm tofu, calcium-set", 100f),
                new("Nutritional yeast, fortified", 12f),
                new("Olive oil", 8f)
            ]),
        Template(
            "Pepperoni Pizza Beans",
            "A regular pizza-beans skillet with white beans, marinara, spinach, mozzarella, pepperoni, and olive oil.",
            MealType.Dinner,
            80,
            [],
            [CyclePhase.Ovulatory, CyclePhase.Luteal],
            [
                new("Cannellini beans, cooked", 220f),
                new("Marinara sauce", 150f),
                new("Spinach, raw", 60f),
                new("Mozzarella cheese, part-skim", 50f),
                new("Turkey pepperoni", 25f),
                new("Olive oil", 5f)
            ]),
        Template(
            "Gluten-Free Pizza Beans",
            "Chicken, white beans, marinara, kale, mozzarella, and olive oil for a higher-protein gluten-free pizza-beans dinner.",
            MealType.Dinner,
            90,
            [DietaryTag.GlutenFree],
            [CyclePhase.Menstrual, CyclePhase.Luteal],
            [
                new("Chicken breast, raw", 100f),
                new("Cannellini beans, cooked", 220f),
                new("Marinara sauce", 140f),
                new("Kale, raw", 60f),
                new("Mozzarella cheese, part-skim", 40f),
                new("Olive oil", 6f)
            ]),
        Template(
            "Salmon Sweet Potato Strength Plate",
            "Salmon, sweet potato, kale, and olive oil for vitamin D, B12, potassium, vitamin A, and recovery fats.",
            MealType.Dinner,
            100,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Ovulatory],
            [
                new("Salmon, cooked", 140f),
                new("Sweet potato, baked", 180f),
                new("Kale, raw", 80f),
                new("Olive oil", 7f)
            ]),
        Template(
            "Salmon Bean Recovery Bowl",
            "Salmon, cannellini beans, sweet potato, kale, and olive oil for protein, potassium, vitamin D, and comfort carbs.",
            MealType.Dinner,
            102,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Luteal],
            [
                new("Salmon, cooked", 130f),
                new("Cannellini beans, cooked", 170f),
                new("Sweet potato, baked", 140f),
                new("Kale, raw", 70f),
                new("Olive oil", 6f)
            ]),
        Template(
            "Turkey Pepperoni Egg Plate",
            "Eggs, turkey pepperoni, spinach, sweet potato, and olive oil for a savory regular breakfast with enough fat for vitamin A.",
            MealType.Breakfast,
            32,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Follicular, CyclePhase.Ovulatory],
            [
                new("Eggs, whole, raw", 100f),
                new("Turkey pepperoni", 20f),
                new("Sweet potato, baked", 130f),
                new("Spinach, raw", 60f),
                new("Olive oil", 4f)
            ]),
        Template(
            "Carrot Hummus Crunch",
            "Carrots with hummus and olive oil so beta-carotene has enough fat beside it.",
            MealType.Snack,
            110,
            [DietaryTag.Vegan, DietaryTag.Vegetarian, DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Menstrual, CyclePhase.Follicular, CyclePhase.Luteal],
            [
                new("Carrot, raw", 160f),
                new("Hummus", 80f),
                new("Olive oil", 4f)
            ]),
        Template(
            "Tuna Avocado Protein Plate",
            "Tuna, avocado, red pepper, and olive oil for lean protein, potassium, B12, vitamin C, and healthy fats.",
            MealType.Snack,
            120,
            [DietaryTag.GlutenFree, DietaryTag.LactoseFree],
            [CyclePhase.Ovulatory, CyclePhase.Luteal],
            [
                new("Tuna, canned in water", 100f),
                new("Avocado, raw", 80f),
                new("Red bell pepper, raw", 80f),
                new("Olive oil", 5f)
            ]),
        Template(
            "Greek Yogurt Mineral Bowl",
            "Greek yogurt, blueberries, and pumpkin seeds for calcium, protein, magnesium, zinc, and an easy snack.",
            MealType.Snack,
            130,
            [DietaryTag.Vegetarian, DietaryTag.GlutenFree],
            [CyclePhase.Follicular, CyclePhase.Ovulatory],
            [
                new("Greek yogurt, plain nonfat", 200f),
                new("Blueberries, raw", 100f),
                new("Pumpkin seeds", 20f)
            ])
    ];

    private static SystemMealTemplateDefinition Template(
        string name,
        string description,
        MealType mealType,
        int sortOrder,
        IReadOnlyCollection<DietaryTag> dietaryTags,
        IReadOnlyCollection<CyclePhase> phaseTags,
        IReadOnlyCollection<SystemMealTemplateRecipeEntry> entries) =>
        new(name, description, mealType, sortOrder, dietaryTags, phaseTags, entries);

    private sealed record SystemMealTemplateDefinition(
        string Name,
        string Description,
        MealType MealType,
        int SortOrder,
        IReadOnlyCollection<DietaryTag> DietaryTags,
        IReadOnlyCollection<CyclePhase> PhaseTags,
        IReadOnlyCollection<SystemMealTemplateRecipeEntry> Entries);

    private sealed record SystemMealTemplateRecipeEntry(string FoodName, float Grams);

    private static string TagsToString<T>(IEnumerable<T> tags)
        where T : struct, Enum =>
        string.Join(",", tags.Select(tag => tag.ToString()));

    private async Task SeedStarterFoodItemsAsync()
    {
        var now = DateTime.UtcNow;
        var existingFoods = await FoodItems.ToListAsync();
        var foods = new List<FoodItem>();
        var repairedExistingFood = false;

        foreach (var starterFood in BuildStarterFoodItems(now))
        {
            var existing = FindExistingStarterFood(existingFoods, starterFood);
            if (existing is null)
            {
                foods.Add(starterFood);
                existingFoods.Add(starterFood);
                continue;
            }

            if (!HasCalories(existing) ||
                NeedsStarterMetadata(existing) ||
                string.Equals(existing.DataType, "Starter", StringComparison.OrdinalIgnoreCase))
            {
                ApplyStarterNutrition(existing, starterFood, existingFoods, now);
                repairedExistingFood = true;
            }
        }

        if (foods.Count > 0)
            await FoodItems.AddRangeAsync(foods);

        if (foods.Count > 0 || repairedExistingFood)
            await SaveChangesAsync();
    }

    private static FoodItem? FindExistingStarterFood(IEnumerable<FoodItem> existingFoods, FoodItem starterFood) =>
        existingFoods.FirstOrDefault(existing =>
            string.Equals(existing.Name, starterFood.Name, StringComparison.OrdinalIgnoreCase) ||
            (existing.FdcId.HasValue &&
             starterFood.FdcId.HasValue &&
             existing.FdcId.Value == starterFood.FdcId.Value));

    private static void ApplyStarterNutrition(
        FoodItem existing,
        FoodItem starterFood,
        IReadOnlyCollection<FoodItem> existingFoods,
        DateTime now)
    {
        existing.Calories = starterFood.Calories;
        existing.Protein = starterFood.Protein;
        existing.Carbs = starterFood.Carbs;
        existing.Fats = starterFood.Fats;
        existing.Fiber = starterFood.Fiber;
        existing.Iron = starterFood.Iron;
        existing.VitaminB12 = starterFood.VitaminB12;
        existing.VitaminC = starterFood.VitaminC;
        existing.VitaminD = starterFood.VitaminD;
        existing.VitaminA = starterFood.VitaminA;
        existing.VitaminB6 = starterFood.VitaminB6;
        existing.Folate = starterFood.Folate;
        existing.Calcium = starterFood.Calcium;
        existing.Magnesium = starterFood.Magnesium;
        existing.Zinc = starterFood.Zinc;
        existing.Potassium = starterFood.Potassium;
        ApplyStarterMetadata(existing, starterFood, existingFoods, now);
    }

    private static void ApplyStarterMetadata(
        FoodItem existing,
        FoodItem starterFood,
        IReadOnlyCollection<FoodItem> existingFoods,
        DateTime now)
    {
        existing.DataType ??= starterFood.DataType;
        existing.BrandOwner ??= starterFood.BrandOwner;
        existing.BrandName ??= starterFood.BrandName;
        existing.GtinUpc ??= starterFood.GtinUpc;
        existing.Ingredients ??= starterFood.Ingredients;
        existing.ServingSize ??= starterFood.ServingSize;
        existing.ServingSizeUnit ??= starterFood.ServingSizeUnit;
        existing.ServingOptionsJson ??= starterFood.ServingOptionsJson;
        existing.UpdatedAt = now;

        if (existing.FdcId is null &&
            starterFood.FdcId.HasValue &&
            existingFoods.All(food => food.Id == existing.Id || food.FdcId != starterFood.FdcId))
        {
            existing.FdcId = starterFood.FdcId;
        }
    }

    private static bool HasCalories(FoodItem food) =>
        food.Calories > 0f;

    private static bool NeedsStarterMetadata(FoodItem food) =>
        string.IsNullOrWhiteSpace(food.DataType);

    private static FoodItem Food(
        DateTime now,
        string name,
        float calories,
        float protein = 0f,
        float carbs = 0f,
        float fats = 0f,
        float fiber = 0f,
        float iron = 0f,
        float vitaminB12 = 0f,
        float vitaminC = 0f,
        float vitaminD = 0f,
        float vitaminA = 0f,
        float vitaminB6 = 0f,
        float folate = 0f,
        float calcium = 0f,
        float magnesium = 0f,
        float zinc = 0f,
        float potassium = 0f,
        int? fdcId = null) =>
        new()
        {
            Name = name,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fats = fats,
            Fiber = fiber,
            Iron = iron,
            VitaminB12 = vitaminB12,
            VitaminC = vitaminC,
            VitaminD = vitaminD,
            VitaminA = vitaminA,
            VitaminB6 = vitaminB6,
            Folate = folate,
            Calcium = calcium,
            Magnesium = magnesium,
            Zinc = zinc,
            Potassium = potassium,
            FdcId = fdcId,
            DataType = "Starter",
            IsCustom = false,
            ServingSize = 100f,
            ServingSizeUnit = "g",
            CreatedAt = now,
            UpdatedAt = now
        };

    private static List<FoodItem> BuildStarterFoodItems(DateTime now) =>
    [
        Food(now, "Carrot, raw", 41f, 0.9f, 9.6f, 0.2f, 2.8f, 0.3f, vitaminC: 5.9f, vitaminA: 835f, calcium: 33f, magnesium: 12f, zinc: 0.2f, potassium: 320f),
        Food(now, "Olive oil", 884f, fats: 100f, fdcId: 172187),
        Food(now, "Oats, rolled, dry", 389f, 16.9f, 66.3f, 6.9f, 10.6f, 4.7f, folate: 56f, calcium: 54f, magnesium: 177f, zinc: 3.97f, potassium: 429f, fdcId: 173904),
        Food(now, "Certified gluten-free rolled oats, dry", 389f, 16.9f, 66.3f, 6.9f, 10.6f, 4.7f, folate: 56f, calcium: 54f, magnesium: 177f, zinc: 3.97f, potassium: 429f),
        Food(now, "Eggs, whole, raw", 143f, 12.6f, 0.7f, 9.5f, iron: 1.75f, vitaminB12: 0.9f, vitaminD: 2f, vitaminA: 160f, calcium: 56f, zinc: 1.29f, potassium: 138f, fdcId: 174161),
        Food(now, "Chicken breast, raw", 120f, 22.5f, fats: 2.6f, iron: 0.37f, calcium: 11f, potassium: 256f, fdcId: 331960),
        Food(now, "Spinach, raw", 23f, 2.9f, 3.6f, 0.4f, 2.2f, 2.7f, vitaminC: 28.1f, vitaminA: 469f, folate: 194f, calcium: 99f, magnesium: 79f, zinc: 0.53f, potassium: 558f, fdcId: 173428),
        Food(now, "Cannellini beans, cooked", 114f, 7.3f, 20.5f, 0.5f, 6.3f, 2.5f, folate: 81f, calcium: 46f, magnesium: 53f, zinc: 1.1f, potassium: 561f),
        Food(now, "Black beans, cooked", 132f, 8.9f, 23.7f, 0.5f, 8.7f, 2.1f, folate: 149f, calcium: 27f, magnesium: 70f, zinc: 1.1f, potassium: 355f),
        Food(now, "Chickpeas, cooked", 164f, 8.9f, 27.4f, 2.6f, 7.6f, 2.9f, vitaminB6: 0.14f, folate: 172f, calcium: 49f, magnesium: 48f, zinc: 1.5f, potassium: 291f),
        Food(now, "Lentils, cooked", 116f, 9f, 20f, 0.4f, 7.9f, 3.3f, vitaminB6: 0.18f, folate: 181f, calcium: 19f, magnesium: 36f, zinc: 1.27f, potassium: 369f),
        Food(now, "Quinoa, cooked", 120f, 4.4f, 21.3f, 1.9f, 2.8f, 1.5f, vitaminB6: 0.12f, folate: 42f, calcium: 17f, magnesium: 64f, zinc: 1.1f, potassium: 172f),
        Food(now, "Marinara sauce", 54f, 1.5f, 8.4f, 1.5f, 2f, 1f, vitaminC: 8f, vitaminA: 30f, calcium: 30f, magnesium: 14f, zinc: 0.3f, potassium: 360f),
        Food(now, "Tomato paste", 82f, 4.3f, 18.9f, 0.5f, 4.1f, 3f, vitaminC: 21.9f, vitaminA: 76f, folate: 11f, calcium: 36f, magnesium: 42f, zinc: 0.6f, potassium: 1014f),
        Food(now, "Mozzarella cheese, part-skim", 254f, 24f, 2.8f, 15.9f, iron: 0.2f, vitaminB12: 1.7f, vitaminD: 0.3f, vitaminA: 180f, calcium: 782f, zinc: 3.2f, potassium: 84f),
        Food(now, "Parmesan cheese", 431f, 38f, 4.1f, 29f, iron: 0.8f, vitaminB12: 1.4f, vitaminD: 0.5f, vitaminA: 207f, calcium: 1184f, zinc: 2.8f, potassium: 92f),
        Food(now, "Turkey pepperoni", 494f, 19f, 4f, 44f, iron: 1.6f, vitaminB12: 1f, vitaminB6: 0.24f, zinc: 2.6f, potassium: 276f),
        Food(now, "Firm tofu, calcium-set", 144f, 17.3f, 2.8f, 8.7f, 2.3f, 2.7f, calcium: 683f, magnesium: 58f, zinc: 1.6f, potassium: 237f),
        Food(now, "Nutritional yeast, fortified", 325f, 45f, 35f, 5f, 20f, 3f, vitaminB12: 44f, vitaminB6: 20f, folate: 1200f, calcium: 67f, magnesium: 180f, zinc: 7f, potassium: 955f),
        Food(now, "Red bell pepper, raw", 31f, 1f, 6f, 0.3f, 2.1f, 0.4f, vitaminC: 128f, vitaminA: 157f, vitaminB6: 0.29f, folate: 46f, calcium: 7f, magnesium: 12f, zinc: 0.25f, potassium: 211f),
        Food(now, "Kale, raw", 35f, 2.9f, 4.4f, 1.5f, 4.1f, 1.6f, vitaminC: 93f, vitaminA: 241f, folate: 62f, calcium: 254f, magnesium: 33f, zinc: 0.4f, potassium: 348f),
        Food(now, "Sweet potato, baked", 90f, 2f, 20.7f, 0.2f, 3.3f, 0.7f, vitaminC: 19.6f, vitaminA: 961f, vitaminB6: 0.29f, folate: 6f, calcium: 38f, magnesium: 27f, zinc: 0.32f, potassium: 475f),
        Food(now, "Avocado, raw", 160f, 2f, 8.5f, 14.7f, 6.7f, 0.6f, vitaminC: 10f, vitaminB6: 0.26f, folate: 81f, calcium: 12f, magnesium: 29f, zinc: 0.64f, potassium: 485f),
        Food(now, "Greek yogurt, plain nonfat", 59f, 10.2f, 3.6f, 0.4f, vitaminB12: 0.75f, calcium: 110f, magnesium: 11f, zinc: 0.52f, potassium: 141f),
        Food(now, "Cottage cheese, low-fat", 82f, 11.5f, 3.4f, 2.3f, vitaminB12: 0.43f, calcium: 83f, magnesium: 8f, zinc: 0.4f, potassium: 104f),
        Food(now, "Blueberries, raw", 57f, 0.7f, 14.5f, 0.3f, 2.4f, 0.3f, vitaminC: 9.7f, vitaminB6: 0.05f, folate: 6f, calcium: 6f, magnesium: 6f, zinc: 0.16f, potassium: 77f),
        Food(now, "Pumpkin seeds", 559f, 30f, 10.7f, 49f, 6f, 8.8f, calcium: 46f, magnesium: 592f, zinc: 7.8f, potassium: 809f),
        Food(now, "Salmon, cooked", 206f, 22f, fats: 12.4f, iron: 0.3f, vitaminB12: 3.2f, vitaminD: 10.9f, vitaminB6: 0.6f, calcium: 9f, magnesium: 30f, zinc: 0.6f, potassium: 384f),
        Food(now, "Tuna, canned in water", 116f, 25.5f, fats: 0.8f, iron: 1f, vitaminB12: 2.2f, vitaminD: 2f, vitaminB6: 0.4f, calcium: 11f, magnesium: 30f, zinc: 0.8f, potassium: 237f),
        Food(now, "Hummus", 166f, 7.9f, 14.3f, 9.6f, 6f, 2.4f, vitaminB6: 0.2f, folate: 83f, calcium: 38f, magnesium: 71f, zinc: 1.4f, potassium: 228f),
        Food(now, "Chia seeds", 486f, 16.5f, 42f, 30.7f, 34.4f, 7.7f, calcium: 631f, magnesium: 335f, zinc: 4.6f, potassium: 407f),
        Food(now, "Fortified soy milk, unsweetened", 33f, 3.3f, 1.6f, 1.8f, 0.6f, 0.6f, vitaminB12: 1.2f, vitaminD: 1.2f, calcium: 123f, magnesium: 15f, zinc: 0.3f, potassium: 118f)
    ];
}
