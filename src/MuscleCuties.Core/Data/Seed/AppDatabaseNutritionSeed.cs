using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private async Task SeedStarterFoodItemsAsync()
    {
        var now = DateTime.UtcNow;
        var starterFoods = BuildStarterFoodItems(now);
        var starterNames = starterFoods
            .Select(food => food.Name.ToUpperInvariant())
            .ToArray();
        var starterFdcIds = starterFoods
            .Where(food => food.FdcId.HasValue)
            .Select(food => food.FdcId!.Value)
            .ToArray();
        // The local FDC catalog can grow large. Only starter candidates and
        // potential FDC-ID collisions are needed for the repair pass.
        var existingFoods = await FoodItems
            .Where(food => starterNames.Contains(food.Name.ToUpper()) ||
                           (food.FdcId.HasValue && starterFdcIds.Contains(food.FdcId.Value)))
            .ToListAsync();
        var foods = new List<FoodItem>();
        var repairedExistingFood = false;

        foreach (var starterFood in starterFoods)
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

    private async Task SeedStarterMealTemplatesAsync()
    {
        if (await MealTemplates.AnyAsync(template => template.IsSystem))
            return;

        var now = DateTime.UtcNow;
        var starterTemplates = BuildStarterMealTemplates();
        var ingredientNames = starterTemplates
            .SelectMany(template => template.Entries)
            .Select(entry => entry.FoodName.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var foodItems = await FoodItems
            .Where(food => ingredientNames.Contains(food.Name.ToUpper()))
            .ToListAsync();
        var templates = starterTemplates
            .Select((template, index) => CreateMealTemplate(template, index + 1, foodItems, now))
            .ToList();

        await MealTemplates.AddRangeAsync(templates);
        await SaveChangesAsync();
    }

    private static MealTemplate CreateMealTemplate(
        StarterMealTemplate template,
        int sortOrder,
        IReadOnlyCollection<FoodItem> foodItems,
        DateTime now)
    {
        var mealTemplate = new MealTemplate
        {
            Name = template.Name,
            Description = template.Description,
            MealType = template.MealType,
            DietaryTags = template.DietaryTags,
            PhaseTags = template.PhaseTags,
            SortOrder = sortOrder,
            IsSystem = true,
            CreatedAt = now
        };

        foreach (var entry in template.Entries)
        {
            var food = foodItems.FirstOrDefault(item =>
                string.Equals(item.Name, entry.FoodName, StringComparison.OrdinalIgnoreCase));
            if (food is null)
                continue;

            mealTemplate.Entries.Add(new MealTemplateEntry
            {
                FoodItemId = food.Id,
                Grams = entry.Grams
            });
        }

        return mealTemplate;
    }

    private static FoodItem? FindExistingStarterFood(IEnumerable<FoodItem> existingFoods, FoodItem starterFood)
    {
        return existingFoods.FirstOrDefault(existing =>
            string.Equals(existing.Name, starterFood.Name, StringComparison.OrdinalIgnoreCase) ||
            (existing.FdcId.HasValue &&
             starterFood.FdcId.HasValue &&
             existing.FdcId.Value == starterFood.FdcId.Value));
    }

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
            existing.FdcId = starterFood.FdcId;
    }

    private static bool HasCalories(FoodItem food)
    {
        return food.Calories > 0f;
    }

    private static bool NeedsStarterMetadata(FoodItem food)
    {
        return string.IsNullOrWhiteSpace(food.DataType);
    }

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
        int? fdcId = null)
    {
        return new FoodItem
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
    }

    private static List<FoodItem> BuildStarterFoodItems(DateTime now)
    {
        return
        [
            Food(now, "Carrot, raw", 41f, 0.9f, 9.6f, 0.2f, 2.8f, 0.3f, vitaminC: 5.9f, vitaminA: 835f, calcium: 33f,
                magnesium: 12f, zinc: 0.2f, potassium: 320f),
            Food(now, "Olive oil", 884f, fats: 100f, fdcId: 172187),
            Food(now, "Oats, rolled, dry", 389f, 16.9f, 66.3f, 6.9f, 10.6f, 4.7f, folate: 56f, calcium: 54f,
                magnesium: 177f, zinc: 3.97f, potassium: 429f, fdcId: 173904),
            Food(now, "Certified gluten-free rolled oats, dry", 389f, 16.9f, 66.3f, 6.9f, 10.6f, 4.7f, folate: 56f,
                calcium: 54f, magnesium: 177f, zinc: 3.97f, potassium: 429f),
            Food(now, "Eggs, whole, raw", 143f, 12.6f, 0.7f, 9.5f, iron: 1.75f, vitaminB12: 0.9f, vitaminD: 2f,
                vitaminA: 160f, calcium: 56f, zinc: 1.29f, potassium: 138f, fdcId: 174161),
            Food(now, "Chicken breast, raw", 120f, 22.5f, fats: 2.6f, iron: 0.37f, calcium: 11f, potassium: 256f,
                fdcId: 331960),
            Food(now, "Spinach, raw", 23f, 2.9f, 3.6f, 0.4f, 2.2f, 2.7f, vitaminC: 28.1f, vitaminA: 469f, folate: 194f,
                calcium: 99f, magnesium: 79f, zinc: 0.53f, potassium: 558f, fdcId: 173428),
            Food(now, "Cannellini beans, cooked", 114f, 7.3f, 20.5f, 0.5f, 6.3f, 2.5f, folate: 81f, calcium: 46f,
                magnesium: 53f, zinc: 1.1f, potassium: 561f),
            Food(now, "Black beans, cooked", 132f, 8.9f, 23.7f, 0.5f, 8.7f, 2.1f, folate: 149f, calcium: 27f,
                magnesium: 70f, zinc: 1.1f, potassium: 355f),
            Food(now, "Chickpeas, cooked", 164f, 8.9f, 27.4f, 2.6f, 7.6f, 2.9f, vitaminB6: 0.14f, folate: 172f,
                calcium: 49f, magnesium: 48f, zinc: 1.5f, potassium: 291f),
            Food(now, "Lentils, cooked", 116f, 9f, 20f, 0.4f, 7.9f, 3.3f, vitaminB6: 0.18f, folate: 181f, calcium: 19f,
                magnesium: 36f, zinc: 1.27f, potassium: 369f),
            Food(now, "Quinoa, cooked", 120f, 4.4f, 21.3f, 1.9f, 2.8f, 1.5f, vitaminB6: 0.12f, folate: 42f,
                calcium: 17f, magnesium: 64f, zinc: 1.1f, potassium: 172f),
            Food(now, "Marinara sauce", 54f, 1.5f, 8.4f, 1.5f, 2f, 1f, vitaminC: 8f, vitaminA: 30f, calcium: 30f,
                magnesium: 14f, zinc: 0.3f, potassium: 360f),
            Food(now, "Tomato paste", 82f, 4.3f, 18.9f, 0.5f, 4.1f, 3f, vitaminC: 21.9f, vitaminA: 76f, folate: 11f,
                calcium: 36f, magnesium: 42f, zinc: 0.6f, potassium: 1014f),
            Food(now, "Mozzarella cheese, part-skim", 254f, 24f, 2.8f, 15.9f, iron: 0.2f, vitaminB12: 1.7f,
                vitaminD: 0.3f, vitaminA: 180f, calcium: 782f, zinc: 3.2f, potassium: 84f),
            Food(now, "Parmesan cheese", 431f, 38f, 4.1f, 29f, iron: 0.8f, vitaminB12: 1.4f, vitaminD: 0.5f,
                vitaminA: 207f, calcium: 1184f, zinc: 2.8f, potassium: 92f),
            Food(now, "Turkey pepperoni", 494f, 19f, 4f, 44f, iron: 1.6f, vitaminB12: 1f, vitaminB6: 0.24f, zinc: 2.6f,
                potassium: 276f),
            Food(now, "Firm tofu, calcium-set", 144f, 17.3f, 2.8f, 8.7f, 2.3f, 2.7f, calcium: 683f, magnesium: 58f,
                zinc: 1.6f, potassium: 237f),
            Food(now, "Nutritional yeast, fortified", 325f, 45f, 35f, 5f, 20f, 3f, 44f, vitaminB6: 20f,
                folate: 1200f, calcium: 67f, magnesium: 180f, zinc: 7f, potassium: 955f),
            Food(now, "Red bell pepper, raw", 31f, 1f, 6f, 0.3f, 2.1f, 0.4f, vitaminC: 128f, vitaminA: 157f,
                vitaminB6: 0.29f, folate: 46f, calcium: 7f, magnesium: 12f, zinc: 0.25f, potassium: 211f),
            Food(now, "Kale, raw", 35f, 2.9f, 4.4f, 1.5f, 4.1f, 1.6f, vitaminC: 93f, vitaminA: 241f, folate: 62f,
                calcium: 254f, magnesium: 33f, zinc: 0.4f, potassium: 348f),
            Food(now, "Sweet potato, baked", 90f, 2f, 20.7f, 0.2f, 3.3f, 0.7f, vitaminC: 19.6f, vitaminA: 961f,
                vitaminB6: 0.29f, folate: 6f, calcium: 38f, magnesium: 27f, zinc: 0.32f, potassium: 475f),
            Food(now, "Avocado, raw", 160f, 2f, 8.5f, 14.7f, 6.7f, 0.6f, vitaminC: 10f, vitaminB6: 0.26f, folate: 81f,
                calcium: 12f, magnesium: 29f, zinc: 0.64f, potassium: 485f),
            Food(now, "Greek yogurt, plain nonfat", 59f, 10.2f, 3.6f, 0.4f, vitaminB12: 0.75f, calcium: 110f,
                magnesium: 11f, zinc: 0.52f, potassium: 141f),
            Food(now, "Cottage cheese, low-fat", 82f, 11.5f, 3.4f, 2.3f, vitaminB12: 0.43f, calcium: 83f, magnesium: 8f,
                zinc: 0.4f, potassium: 104f),
            Food(now, "Blueberries, raw", 57f, 0.7f, 14.5f, 0.3f, 2.4f, 0.3f, vitaminC: 9.7f, vitaminB6: 0.05f,
                folate: 6f, calcium: 6f, magnesium: 6f, zinc: 0.16f, potassium: 77f),
            Food(now, "Pumpkin seeds", 559f, 30f, 10.7f, 49f, 6f, 8.8f, calcium: 46f, magnesium: 592f, zinc: 7.8f,
                potassium: 809f),
            Food(now, "Salmon, cooked", 206f, 22f, fats: 12.4f, iron: 0.3f, vitaminB12: 3.2f, vitaminD: 10.9f,
                vitaminB6: 0.6f, calcium: 9f, magnesium: 30f, zinc: 0.6f, potassium: 384f),
            Food(now, "Tuna, canned in water", 116f, 25.5f, fats: 0.8f, iron: 1f, vitaminB12: 2.2f, vitaminD: 2f,
                vitaminB6: 0.4f, calcium: 11f, magnesium: 30f, zinc: 0.8f, potassium: 237f),
            Food(now, "Hummus", 166f, 7.9f, 14.3f, 9.6f, 6f, 2.4f, vitaminB6: 0.2f, folate: 83f, calcium: 38f,
                magnesium: 71f, zinc: 1.4f, potassium: 228f),
            Food(now, "Chia seeds", 486f, 16.5f, 42f, 30.7f, 34.4f, 7.7f, calcium: 631f, magnesium: 335f, zinc: 4.6f,
                potassium: 407f),
            Food(now, "Fortified soy milk, unsweetened", 33f, 3.3f, 1.6f, 1.8f, 0.6f, 0.6f, 1.2f,
                vitaminD: 1.2f, calcium: 123f, magnesium: 15f, zinc: 0.3f, potassium: 118f),

            // Carb bases
            Food(now, "White potato, baked", 93f, 2.5f, 21.2f, 0.1f, 2.2f, 0.6f, vitaminC: 9.6f,
                vitaminB6: 0.3f, calcium: 15f, magnesium: 28f, zinc: 0.3f, potassium: 544f),
            Food(now, "Whole wheat bread", 252f, 12.5f, 43f, 3.4f, 6f, 2.5f, folate: 44f,
                calcium: 107f, magnesium: 75f, zinc: 1.8f, potassium: 254f),
            Food(now, "Flour tortilla, whole wheat", 291f, 8f, 46f, 8.5f, 4.6f, 2.7f,
                calcium: 180f, magnesium: 38f, zinc: 0.9f, potassium: 170f),
            Food(now, "Hamburger bun, whole wheat", 260f, 9f, 46f, 4.5f, 4f, 2.5f, folate: 70f,
                calcium: 120f, magnesium: 42f, zinc: 1.1f, potassium: 180f),
            Food(now, "Pinto beans, cooked", 143f, 9f, 26.2f, 0.6f, 9f, 2.1f, vitaminB6: 0.23f, folate: 172f,
                calcium: 46f, magnesium: 50f, zinc: 0.98f, potassium: 436f),
            Food(now, "Brown rice, cooked", 123f, 2.7f, 25.6f, 1f, 1.6f, 0.6f, vitaminB6: 0.15f,
                magnesium: 39f, zinc: 0.6f, potassium: 86f),

            // Protein bases
            Food(now, "Turkey ham, sliced", 120f, 18f, 1.5f, 4.5f, iron: 0.8f, vitaminB12: 0.8f,
                vitaminB6: 0.3f, zinc: 1.8f, potassium: 260f),
            Food(now, "Turkey sausage", 196f, 17f, 1f, 13.5f, iron: 1.2f, vitaminB12: 1.2f,
                vitaminB6: 0.25f, zinc: 2.5f, potassium: 230f),
            Food(now, "Ground turkey, raw", 149f, 19.5f, fats: 7.7f, iron: 1.1f, vitaminB12: 1.5f,
                vitaminB6: 0.5f, zinc: 3.2f, potassium: 280f),
            Food(now, "Ground beef, 90% lean", 176f, 20f, fats: 10f, iron: 2.3f, vitaminB12: 2.3f,
                vitaminB6: 0.35f, zinc: 4.8f, potassium: 315f),
            Food(now, "Ground chicken, raw", 143f, 17.4f, fats: 8.1f, iron: 0.8f, vitaminB12: 0.3f,
                vitaminB6: 0.4f, zinc: 1.5f, potassium: 222f),

            // Vegetables and fruits
            Food(now, "Lettuce, romaine", 17f, 1.2f, 3.3f, 0.3f, 2.1f, 1f, vitaminC: 24f, vitaminA: 436f,
                folate: 136f, calcium: 33f, magnesium: 14f, zinc: 0.2f, potassium: 247f),
            Food(now, "Onion, raw", 40f, 1.1f, 9.3f, 0.1f, 1.7f, 0.2f, vitaminC: 7.4f,
                vitaminB6: 0.12f, folate: 19f, calcium: 23f, magnesium: 10f, zinc: 0.17f, potassium: 146f),
            Food(now, "Tomato, raw", 18f, 0.9f, 3.9f, 0.2f, 1.2f, 0.3f, vitaminC: 14f, vitaminA: 42f,
                vitaminB6: 0.08f, folate: 15f, calcium: 10f, magnesium: 11f, zinc: 0.17f, potassium: 237f),
            Food(now, "Strawberries, raw", 32f, 0.7f, 7.7f, 0.3f, 2f, 0.4f, vitaminC: 58.8f,
                folate: 24f, calcium: 16f, magnesium: 13f, zinc: 0.14f, potassium: 153f),
            Food(now, "Raspberries, raw", 52f, 1.2f, 11.9f, 0.7f, 6.5f, 0.7f, vitaminC: 26.2f,
                folate: 21f, calcium: 25f, magnesium: 22f, zinc: 0.42f, potassium: 151f),
            Food(now, "Banana, raw", 89f, 1.1f, 22.8f, 0.3f, 2.6f, 0.3f, vitaminC: 8.7f,
                vitaminB6: 0.37f, folate: 20f, calcium: 5f, magnesium: 27f, zinc: 0.15f, potassium: 358f),

            // Sauces and condiments
            Food(now, "Crushed tomatoes, canned", 32f, 1.6f, 6.3f, 0.3f, 1.9f, 0.8f, vitaminC: 9f, vitaminA: 25f,
                calcium: 22f, magnesium: 15f, zinc: 0.2f, potassium: 293f),
            Food(now, "Sriracha sauce", 93f, 1.7f, 18.5f, 1f, 1f, 0.5f, vitaminC: 44f, vitaminA: 130f,
                calcium: 12f, potassium: 115f),
            Food(now, "Sour cream, reduced fat", 135f, 3.5f, 5f, 11.5f, vitaminB12: 0.3f,
                calcium: 116f, magnesium: 10f, zinc: 0.3f, potassium: 140f),
            Food(now, "Yellow mustard", 60f, 3.7f, 5.3f, 3.3f, 3.3f, 1.5f,
                calcium: 58f, magnesium: 48f, zinc: 0.6f, potassium: 138f),
            Food(now, "Ketchup, zero sugar", 15f, 0.2f, 3.5f, 0.1f, 0.1f,
                potassium: 100f),
            Food(now, "Hot sauce, generic", 11f, 0.5f, 1.8f, 0.4f, 0.5f, 0.6f, vitaminC: 12f, vitaminA: 54f,
                calcium: 10f, potassium: 115f),
            Food(now, "Greek yogurt, plain 2%", 73f, 10f, 4.5f, 1.5f, vitaminB12: 0.75f,
                calcium: 115f, magnesium: 11f, zinc: 0.52f, potassium: 141f),

            // Sweet breakfast items
            Food(now, "Honey", 304f, 0.3f, 82.4f, fats: 0f, potassium: 52f,
                calcium: 6f, magnesium: 2f, zinc: 0.22f),
            Food(now, "Peanut butter, natural", 588f, 25f, 20f, 50f, 6f, 1.9f,
                vitaminB6: 0.44f, folate: 87f, calcium: 43f, magnesium: 168f, zinc: 2.8f, potassium: 649f),
            Food(now, "Dark chocolate, 70%", 598f, 7.8f, 46f, 43f, 11f, 11.9f,
                calcium: 73f, magnesium: 228f, zinc: 3.3f, potassium: 715f),
            Food(now, "Maple syrup", 260f, 0.1f, 67f, 0.1f, iron: 0.1f,
                calcium: 102f, magnesium: 21f, zinc: 1.5f, potassium: 212f)
        ];
    }

    private static IReadOnlyList<StarterMealTemplate> BuildStarterMealTemplates()
    {
        return
        [
            Template("Margherita Pizza Beans", "Cannellini beans with marinara, mozzarella, parmesan, and olive oil.",
                MealType.Lunch, "Regular,Vegetarian", "Follicular,Ovulatory,Luteal",
                Entry("Cannellini beans, cooked", 180f), Entry("Marinara sauce", 90f),
                Entry("Mozzarella cheese, part-skim", 35f), Entry("Parmesan cheese", 8f),
                Entry("Olive oil", 8f)),
            Template("Vegan Pizza Beans", "Beans, tomato, peppers, and fortified nutritional yeast.",
                MealType.Lunch, "Vegan,Vegetarian,DairyFree", "Menstrual,Follicular,Luteal",
                Entry("Cannellini beans, cooked", 180f), Entry("Tomato paste", 35f),
                Entry("Red bell pepper, raw", 80f), Entry("Nutritional yeast, fortified", 16f),
                Entry("Olive oil", 8f)),
            Template("Pepperoni Pizza Beans", "High-protein pizza beans with turkey pepperoni and cheese.",
                MealType.Dinner, "Regular", "Ovulatory,Luteal",
                Entry("Cannellini beans, cooked", 170f), Entry("Marinara sauce", 90f),
                Entry("Turkey pepperoni", 25f), Entry("Mozzarella cheese, part-skim", 35f),
                Entry("Olive oil", 6f)),
            Template("Gluten-Free Pizza Beans", "Pizza-style beans without gluten-containing grains.",
                MealType.Dinner, "GlutenFree,Vegetarian", "Menstrual,Follicular,Luteal",
                Entry("Cannellini beans, cooked", 180f), Entry("Marinara sauce", 90f),
                Entry("Mozzarella cheese, part-skim", 35f), Entry("Kale, raw", 60f),
                Entry("Olive oil", 8f)),
            Template("Chicken Quinoa Power Bowl", "Chicken, quinoa, spinach, pepper, and avocado.",
                MealType.Lunch, "Regular,GlutenFree", "Follicular,Ovulatory",
                Entry("Chicken breast, raw", 140f), Entry("Quinoa, cooked", 150f),
                Entry("Spinach, raw", 70f), Entry("Red bell pepper, raw", 80f),
                Entry("Avocado, raw", 50f)),
            Template("Salmon Sweet Potato Plate", "Salmon with sweet potato, kale, and olive oil.",
                MealType.Dinner, "Regular,GlutenFree", "Menstrual,Luteal",
                Entry("Salmon, cooked", 130f), Entry("Sweet potato, baked", 180f),
                Entry("Kale, raw", 70f), Entry("Olive oil", 8f)),
            Template("Tofu Lentil Iron Bowl", "Calcium-set tofu, lentils, spinach, and tomato.",
                MealType.Dinner, "Vegan,Vegetarian,GlutenFree,DairyFree", "Menstrual,Follicular",
                Entry("Firm tofu, calcium-set", 140f), Entry("Lentils, cooked", 150f),
                Entry("Spinach, raw", 80f), Entry("Tomato, raw", 100f)),
            Template("Turkey Burger Bowl", "Ground turkey, potato, lettuce, tomato, and mustard.",
                MealType.Dinner, "Regular,GlutenFree", "Ovulatory,Luteal",
                Entry("Ground turkey, raw", 140f), Entry("White potato, baked", 180f),
                Entry("Lettuce, romaine", 80f), Entry("Tomato, raw", 100f),
                Entry("Yellow mustard", 12f)),
            Template("Greek Yogurt Berry Crunch", "Greek yogurt with berries, chia, and honey.",
                MealType.Breakfast, "Vegetarian,GlutenFree", "Follicular,Ovulatory",
                Entry("Greek yogurt, plain nonfat", 220f), Entry("Blueberries, raw", 90f),
                Entry("Chia seeds", 18f), Entry("Honey", 10f)),
            Template("Oats Peanut Butter Berry Bowl", "Oats, berries, peanut butter, and fortified soy milk.",
                MealType.Breakfast, "Vegan,Vegetarian,DairyFree", "Luteal,Follicular",
                Entry("Oats, rolled, dry", 55f), Entry("Fortified soy milk, unsweetened", 180f),
                Entry("Peanut butter, natural", 18f), Entry("Strawberries, raw", 100f)),
            Template("Gluten-Free Oat Protein Bowl", "Gluten-free oats with soy milk, blueberries, and chia.",
                MealType.Breakfast, "Vegan,Vegetarian,GlutenFree,DairyFree", "Follicular,Luteal",
                Entry("Certified gluten-free rolled oats, dry", 55f),
                Entry("Fortified soy milk, unsweetened", 180f), Entry("Blueberries, raw", 90f),
                Entry("Chia seeds", 16f)),
            Template("Egg Avocado Toast", "Whole wheat toast with egg, avocado, and tomato.",
                MealType.Breakfast, "Vegetarian", "Ovulatory,Follicular",
                Entry("Whole wheat bread", 70f), Entry("Eggs, whole, raw", 100f),
                Entry("Avocado, raw", 60f), Entry("Tomato, raw", 80f)),
            Template("Tuna Hummus Wrap", "Whole wheat tortilla with tuna, hummus, lettuce, and pepper.",
                MealType.Lunch, "Regular", "Ovulatory",
                Entry("Tuna, canned in water", 120f), Entry("Flour tortilla, whole wheat", 70f),
                Entry("Hummus", 45f), Entry("Lettuce, romaine", 60f),
                Entry("Red bell pepper, raw", 70f)),
            Template("Black Bean Quinoa Taco Bowl", "Black beans, quinoa, avocado, tomato, and hot sauce.",
                MealType.Lunch, "Vegan,Vegetarian,GlutenFree,DairyFree", "Follicular,Luteal",
                Entry("Black beans, cooked", 150f), Entry("Quinoa, cooked", 140f),
                Entry("Avocado, raw", 55f), Entry("Tomato, raw", 100f),
                Entry("Hot sauce, generic", 10f)),
            Template("Cottage Cheese Berry Bowl", "Cottage cheese with berries, pumpkin seeds, and maple.",
                MealType.Snack, "Vegetarian,GlutenFree", "Menstrual,Luteal",
                Entry("Cottage cheese, low-fat", 180f), Entry("Raspberries, raw", 90f),
                Entry("Pumpkin seeds", 16f), Entry("Maple syrup", 8f)),
            Template("Hummus Sweet Potato Plate", "Sweet potato with hummus, spinach, tomato, and olive oil.",
                MealType.Lunch, "Vegan,Vegetarian,GlutenFree,DairyFree", "Menstrual,Luteal",
                Entry("Sweet potato, baked", 180f), Entry("Hummus", 70f),
                Entry("Spinach, raw", 80f), Entry("Tomato, raw", 100f),
                Entry("Olive oil", 6f)),
            Template("Chicken Potato Recovery Plate", "Chicken, potato, kale, and yogurt sauce.",
                MealType.Dinner, "Regular,GlutenFree", "Menstrual,Follicular",
                Entry("Chicken breast, raw", 150f), Entry("White potato, baked", 180f),
                Entry("Kale, raw", 70f), Entry("Greek yogurt, plain 2%", 45f))
        ];
    }

    private static StarterMealTemplate Template(
        string name,
        string description,
        MealType mealType,
        string dietaryTags,
        string phaseTags,
        params StarterMealTemplateEntry[] entries)
    {
        return new StarterMealTemplate(name, description, mealType, dietaryTags, phaseTags, entries);
    }

    private static StarterMealTemplateEntry Entry(string foodName, float grams)
    {
        return new StarterMealTemplateEntry(foodName, grams);
    }

    private sealed record StarterMealTemplate(
        string Name,
        string Description,
        MealType MealType,
        string DietaryTags,
        string PhaseTags,
        IReadOnlyList<StarterMealTemplateEntry> Entries);

    private sealed record StarterMealTemplateEntry(string FoodName, float Grams);

}
