namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>
/// Recipe reference portions and food-specific nutrients per 100 g. Food forms are fixed so a
/// generated meal never inherits the meal target as an ingredient's nutrient density.
/// </summary>
public static class GeneratedMealIngredientCatalog
{
    private static readonly Dictionary<string, GeneratedMealIngredientProfile> Profiles =
        new Dictionary<string, GeneratedMealIngredientProfile>(StringComparer.Ordinal)
        {
            ["Eggs"] = new("Eggs", 100f, 143f, 12.6f, 0.7f, 9.5f),
            ["Whole-Grain Bread"] = new("Whole-Grain Bread", 65f, 252f, 12.5f, 43f, 3.4f),
            ["Spinach"] = new("Spinach", 35f, 23f, 2.9f, 3.6f, 0.4f),
            ["Olive Oil"] = new("Olive Oil", 8f, 884f, 0f, 0f, 100f, true),
            ["Tofu"] = new("Tofu", 140f, 144f, 17.3f, 2.8f, 8.7f),
            ["Bell Peppers"] = new("Bell Peppers", 75f, 31f, 1f, 6f, 0.3f),
            ["Onions"] = new("Onions", 45f, 40f, 1.1f, 9.3f, 0.1f),
            ["Avocado"] = new("Avocado", 65f, 160f, 2f, 8.5f, 14.7f),
            ["Rolled Oats"] = new("Rolled Oats", 65f, 389f, 16.9f, 66.3f, 6.9f),
            ["Protein Powder"] = new("Protein Powder", 30f, 400f, 80f, 7f, 5f),
            ["Mixed Berries"] = new("Mixed Berries", 90f, 50f, 0.8f, 12f, 0.3f),
            ["Chia Seeds"] = new("Chia Seeds", 12f, 486f, 16.5f, 42f, 30.7f),
            ["Greek Yogurt"] = new("Greek Yogurt", 200f, 59f, 10.2f, 3.6f, 0.4f),
            ["Blueberries"] = new("Blueberries", 70f, 57f, 0.7f, 14.5f, 0.3f),
            ["Walnuts"] = new("Walnuts", 20f, 654f, 15.2f, 13.7f, 65.2f),
            ["Honey"] = new("Honey", 15f, 304f, 0.3f, 82.4f, 0f),
            ["Chicken Breast"] = new("Chicken Breast", 145f, 165f, 31f, 0f, 3.6f),
            ["Brown Rice"] = new("Brown Rice", 125f, 123f, 2.7f, 25.6f, 1f),
            ["Cucumber"] = new("Cucumber", 75f, 15f, 0.7f, 3.6f, 0.1f),
            ["Tzatziki"] = new("Tzatziki", 45f, 70f, 3f, 4f, 5f),
            ["Tuna"] = new("Tuna", 145f, 116f, 25.5f, 0f, 0.8f),
            ["Quinoa"] = new("Quinoa", 140f, 120f, 4.4f, 21.3f, 1.9f),
            ["Mixed Greens"] = new("Mixed Greens", 65f, 20f, 2f, 3f, 0.3f),
            ["Cherry Tomatoes"] = new("Cherry Tomatoes", 80f, 18f, 0.9f, 3.9f, 0.2f),
            ["Chickpeas"] = new("Chickpeas", 150f, 164f, 8.9f, 27.4f, 2.6f),
            ["Sweet Potato"] = new("Sweet Potato", 165f, 90f, 2f, 20.7f, 0.2f),
            ["Kale"] = new("Kale", 75f, 35f, 2.9f, 4.4f, 1.5f),
            ["Tahini"] = new("Tahini", 25f, 595f, 17f, 21f, 54f),
            ["Salmon Fillet"] = new("Salmon Fillet", 160f, 206f, 22f, 0f, 12.4f),
            ["Asparagus"] = new("Asparagus", 110f, 20f, 2.2f, 3.9f, 0.1f),
            ["Ground Turkey"] = new("Ground Turkey", 145f, 149f, 19.5f, 0f, 7.7f),
            ["Black Beans"] = new("Black Beans", 95f, 132f, 8.9f, 23.7f, 0.5f),
            ["Tomatoes"] = new("Tomatoes", 80f, 18f, 0.9f, 3.9f, 0.2f),
            ["Edamame"] = new("Edamame", 95f, 121f, 11.9f, 8.9f, 5.2f),
            ["Broccoli"] = new("Broccoli", 90f, 35f, 2.4f, 7.2f, 0.4f),
            ["Jasmine Rice"] = new("Jasmine Rice", 125f, 130f, 2.7f, 28f, 0.3f),
            ["Sesame Oil"] = new("Sesame Oil", 8f, 884f, 0f, 0f, 100f, true),
            ["Apple"] = new("Apple", 160f, 52f, 0.3f, 13.8f, 0.2f),
            ["Peanut Butter"] = new("Peanut Butter", 25f, 588f, 25f, 20f, 50f),
            ["Cottage Cheese"] = new("Cottage Cheese", 180f, 82f, 11.5f, 3.4f, 2.3f),
            ["Raspberries"] = new("Raspberries", 80f, 52f, 1.2f, 11.9f, 0.7f),
            ["Rice Cakes"] = new("Rice Cakes", 35f, 387f, 8.2f, 81.5f, 2.8f),
            ["Sea Salt"] = new("Sea Salt", 1f, 0f, 0f, 0f, 0f)
        };

    public static IReadOnlyList<GeneratedMealIngredientProfile> Resolve(IReadOnlyList<string> ingredientNames)
    {
        var profiles = new List<GeneratedMealIngredientProfile>(ingredientNames.Count);
        foreach (var name in ingredientNames)
        {
            if (!Profiles.TryGetValue(name, out var profile))
                return [];
            profiles.Add(profile);
        }

        return profiles;
    }
}
