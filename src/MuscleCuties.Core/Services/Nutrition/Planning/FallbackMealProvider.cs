using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public interface IFallbackMealProvider
{
    MealPlan GeneratePlan(
        CalorieBudget budget,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury,
        IReadOnlySet<DietaryTag>? dietaryTags = null);

    IReadOnlyList<FallbackMealItem> GetMealsForType(
        MealType mealType,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury,
        IReadOnlySet<DietaryTag>? dietaryTags = null);
}

public sealed class FallbackMealProvider : IFallbackMealProvider
{
    private static readonly string[] MeatTerms =
        ["chicken", "turkey", "beef", "salmon", "fish", "tuna", "shrimp", "pork", "ham", "lamb"];

    private static readonly string[] DairyTerms =
        ["cheese", "yogurt", "milk", "cream", "butter", "whey", "cottage cheese", "parmesan"];

    private static readonly string[] EggTerms = ["egg"];

    private static readonly string[] GlutenTerms =
        ["wheat", "bread", "pasta", "flour", "tortilla", "granola", "oats"];

    public MealPlan GeneratePlan(
        CalorieBudget budget,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury,
        IReadOnlySet<DietaryTag>? dietaryTags = null)
    {
        var entries = new List<MealPlanEntry>(4);

        foreach (var allocation in budget.Allocations)
        {
            var candidates = GetMealsForType(allocation.MealType, breakfastPreference, dietaryTags);
            if (candidates.Count == 0)
                continue;

            var best = PickClosest(candidates, allocation.Calories);
            var scaled = ScaleToTarget(best, allocation);

            entries.Add(new MealPlanEntry(scaled, allocation));
        }

        return new MealPlan(budget, entries);
    }

    public IReadOnlyList<FallbackMealItem> GetMealsForType(
        MealType mealType,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury,
        IReadOnlySet<DietaryTag>? dietaryTags = null)
    {
        var all = mealType switch
        {
            MealType.Breakfast => breakfastPreference is BreakfastPreference.Sweet
                ? SweetBreakfasts
                : SavouryBreakfasts,
            MealType.Lunch => Lunches,
            MealType.Dinner => Dinners,
            MealType.Snack => Snacks,
            _ => []
        };

        if (dietaryTags is null || dietaryTags.Count == 0 ||
            (dietaryTags.Count == 1 && dietaryTags.Contains(DietaryTag.None)))
            return all;

        return all.Where(m => MatchesDietaryTags(m, dietaryTags)).ToList();
    }

    private static bool MatchesDietaryTags(FallbackMealItem meal, IReadOnlySet<DietaryTag> tags)
    {
        foreach (var tag in tags)
        {
            var passes = tag switch
            {
                DietaryTag.Vegan => !ContainsAny(meal.IngredientNames, MeatTerms) &&
                                    !ContainsAny(meal.IngredientNames, DairyTerms) &&
                                    !ContainsAny(meal.IngredientNames, EggTerms),
                DietaryTag.Vegetarian => !ContainsAny(meal.IngredientNames, MeatTerms),
                DietaryTag.GlutenFree => !ContainsAny(meal.IngredientNames, GlutenTerms),
                DietaryTag.LactoseFree => !ContainsAny(meal.IngredientNames, DairyTerms),
                _ => true
            };

            if (!passes)
                return false;
        }

        return true;
    }

    private static bool ContainsAny(IReadOnlyList<string> ingredients, string[] terms)
    {
        foreach (var ingredient in ingredients)
            foreach (var term in terms)
                if (ingredient.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    private static FallbackMealItem PickClosest(IReadOnlyList<FallbackMealItem> candidates, float targetCalories)
    {
        if (candidates.Count == 0)
            throw new ArgumentException("Candidates list must not be empty.", nameof(candidates));

        var best = candidates[0];
        var bestDelta = MathF.Abs(best.Calories - targetCalories);

        for (var i = 1; i < candidates.Count; i++)
        {
            var delta = MathF.Abs(candidates[i].Calories - targetCalories);
            if (delta < bestDelta)
            {
                best = candidates[i];
                bestDelta = delta;
            }
        }

        return best;
    }

    private static FallbackMealItem ScaleToTarget(FallbackMealItem item, MealCalorieAllocation target)
    {
        if (item.Calories <= 0f)
            return item with { Macros = target.Macros };

        var ratio = target.Calories / item.Calories;
        if (MathF.Abs(ratio - 1f) < 0.05f)
            return item;

        return item with
        {
            Macros = new MacroNutrients(
                Calories: RoundTo(item.Macros.Calories * ratio, 1f),
                Protein: RoundTo(item.Macros.Protein * ratio, 1f),
                Carbs: RoundTo(item.Macros.Carbs * ratio, 1f),
                Fats: RoundTo(item.Macros.Fats * ratio, 1f))
        };
    }

    private static float RoundTo(float value, float nearest)
        => MathF.Round(value / nearest) * nearest;

    // ----- Hardcoded balanced meal catalogs (zero network) -----

    private static readonly IReadOnlyList<FallbackMealItem> SavouryBreakfasts =
    [
        Meal("Scrambled Eggs with Toast",
            "Two eggs, whole-grain toast, and a side of greens.",
            MealType.Breakfast, 420f, 28f, 35f, 18f,
            "eggs", "whole-grain bread", "spinach", "olive oil"),

        Meal("Tofu Scramble with Vegetables",
            "Seasoned tofu scramble with peppers, onions, and avocado.",
            MealType.Breakfast, 390f, 22f, 30f, 20f,
            "tofu", "bell pepper", "onion", "avocado", "turmeric"),

        Meal("Chia Pudding with Fruit",
            "Chia pudding with soy yogurt, mango, and almonds.",
            MealType.Breakfast, 370f, 18f, 34f, 18f,
            "chia seeds", "soy yogurt", "mango", "almonds"),

        Meal("Sweet Potato Hash",
            "Roasted sweet potato with tofu, black beans, and salsa.",
            MealType.Breakfast, 400f, 22f, 46f, 14f,
            "sweet potato", "tofu", "black beans", "salsa", "avocado")
    ];

    private static readonly IReadOnlyList<FallbackMealItem> SweetBreakfasts =
    [
        Meal("Berry Smoothie Bowl",
            "Blended berries with soy yogurt, topped with seeds and coconut.",
            MealType.Breakfast, 350f, 18f, 38f, 14f,
            "mixed berries", "soy yogurt", "chia seeds", "coconut flakes"),

        Meal("Banana Oat Pancakes",
            "Banana oat pancakes with soy yogurt and mixed berries.",
            MealType.Breakfast, 380f, 18f, 48f, 14f,
            "banana", "oat flour", "soy yogurt", "mixed berries"),

        Meal("Açaí Bowl",
            "Açaí blend topped with soy yogurt, coconut, and hemp seeds.",
            MealType.Breakfast, 360f, 16f, 42f, 15f,
            "açaí", "soy yogurt", "coconut flakes", "hemp seeds")
    ];

    private static readonly IReadOnlyList<FallbackMealItem> Lunches =
    [
        Meal("Grilled Chicken Salad",
            "Mixed greens with grilled chicken, cherry tomatoes, and vinaigrette.",
            MealType.Lunch, 520f, 42f, 28f, 24f,
            "chicken breast", "mixed greens", "cherry tomatoes", "olive oil", "balsamic vinegar"),

        Meal("Lentil and Vegetable Soup",
            "Hearty lentil soup with tofu, carrots, celery, and spices.",
            MealType.Lunch, 460f, 30f, 42f, 16f,
            "lentils", "tofu", "carrots", "celery", "onion", "cumin"),

        Meal("Salmon Rice Bowl",
            "Grilled salmon over brown rice with steamed broccoli.",
            MealType.Lunch, 560f, 38f, 52f, 20f,
            "salmon fillet", "brown rice", "broccoli", "soy sauce", "sesame seeds"),

        Meal("Chickpea Buddha Bowl",
            "Roasted chickpeas with tofu, vegetables, and tahini.",
            MealType.Lunch, 500f, 28f, 48f, 20f,
            "chickpeas", "tofu", "kale", "sweet potato", "tahini"),

        Meal("Black Bean Burrito Bowl",
            "Black beans with tempeh, peppers, salsa, and avocado.",
            MealType.Lunch, 510f, 30f, 46f, 20f,
            "black beans", "tempeh", "bell pepper", "salsa", "avocado")
    ];

    private static readonly IReadOnlyList<FallbackMealItem> Dinners =
    [
        Meal("Baked Chicken with Sweet Potato",
            "Herb-baked chicken breast with roasted sweet potato and green beans.",
            MealType.Dinner, 480f, 40f, 40f, 14f,
            "chicken breast", "sweet potato", "green beans", "olive oil", "herbs"),

        Meal("Beef Stir-Fry with Rice",
            "Lean beef strips with mixed vegetables over jasmine rice.",
            MealType.Dinner, 530f, 36f, 52f, 18f,
            "lean beef", "bell pepper", "broccoli", "jasmine rice", "soy sauce"),

        Meal("Grilled Fish Tacos",
            "White fish tacos with cabbage slaw and lime crema.",
            MealType.Dinner, 460f, 32f, 40f, 16f,
            "white fish", "corn tortillas", "cabbage", "lime", "avocado"),

        Meal("Tofu Stir-Fry with Rice",
            "Crispy tofu with mixed vegetables and a modest scoop of jasmine rice.",
            MealType.Dinner, 470f, 28f, 42f, 18f,
            "tofu", "bell pepper", "broccoli", "jasmine rice", "soy sauce", "sesame oil"),

        Meal("Stuffed Bell Peppers",
            "Bell peppers stuffed with tofu, black beans, quinoa, and spices.",
            MealType.Dinner, 440f, 26f, 42f, 16f,
            "bell pepper", "tofu", "quinoa", "black beans", "cumin", "tomato sauce")
    ];

    private static readonly IReadOnlyList<FallbackMealItem> Snacks =
    [
        Meal("Apple with Almond Butter",
            "Sliced apple with a tablespoon of almond butter.",
            MealType.Snack, 220f, 6f, 28f, 10f,
            "apple", "almond butter"),

        Meal("Trail Mix",
            "Mixed nuts, seeds, and dried cranberries.",
            MealType.Snack, 250f, 8f, 24f, 14f,
            "almonds", "pumpkin seeds", "dried cranberries"),

        Meal("Hummus with Vegetables",
            "Hummus with carrot sticks and cucumber slices.",
            MealType.Snack, 190f, 8f, 22f, 8f,
            "hummus", "carrots", "cucumber"),

        Meal("Rice Cakes with Avocado",
            "Brown rice cakes topped with mashed avocado and salt.",
            MealType.Snack, 200f, 4f, 24f, 10f,
            "rice cakes", "avocado", "salt")
    ];

    private static FallbackMealItem Meal(
        string name, string description, MealType type,
        float calories, float protein, float carbs, float fats,
        params string[] ingredients)
    {
        return new FallbackMealItem(
            name, description, type,
            new MacroNutrients(calories, protein, carbs, fats),
            ingredients);
    }
}
