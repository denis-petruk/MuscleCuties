using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class IngredientDietaryTests
{
    [Theory]
    [InlineData("Chicken Breast", false)]
    [InlineData("Salmon Fillet", false)]
    [InlineData("Turkey Ham", false)]
    [InlineData("Tofu", true)]
    [InlineData("Brown Rice", true)]
    [InlineData("Broccoli", true)]
    [InlineData("Olive Oil", true)]
    [InlineData("Lentils", true)]
    public void IsVegetarian_ClassifiesCorrectly(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsVegetarian);
    }

    [Theory]
    [InlineData("Chicken Breast", false)]
    [InlineData("Greek Yogurt", false)]
    [InlineData("Cheddar Cheese", false)]
    [InlineData("Eggs", false)]
    [InlineData("Tofu", true)]
    [InlineData("Brown Rice", true)]
    [InlineData("Broccoli", true)]
    [InlineData("Olive Oil", true)]
    [InlineData("Lentils", true)]
    public void IsVegan_ClassifiesCorrectly(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsVegan);
    }

    [Fact]
    public void IsVegan_ImpliesIsVegetarian()
    {
        var veganFoods = new[] { "Tofu", "Brown Rice", "Lentils", "Olive Oil", "Broccoli" };

        foreach (var name in veganFoods)
        {
            var ingredient = MakeIngredient(name);
            if (ingredient.IsVegan)
                Assert.True(ingredient.IsVegetarian, $"{name} is vegan but not vegetarian");
        }
    }

    [Theory]
    [InlineData("Whole Wheat Pasta", false)]
    [InlineData("Bread", false)]
    [InlineData("Couscous", false)]
    [InlineData("Flour Tortilla", false)]
    [InlineData("Brown Rice", true)]
    [InlineData("Quinoa", true)]
    [InlineData("Potato", true)]
    [InlineData("Chicken Breast", true)]
    public void IsGlutenFree_ClassifiesCorrectly(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsGlutenFree);
    }

    [Theory]
    [InlineData("Cheddar Cheese", false)]
    [InlineData("Greek Yogurt", false)]
    [InlineData("Butter", false)]
    [InlineData("Whey Protein", false)]
    [InlineData("Chicken Breast", true)]
    [InlineData("Brown Rice", true)]
    [InlineData("Olive Oil", true)]
    public void IsLactoseFree_ClassifiesCorrectly(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsLactoseFree);
    }

    [Theory]
    [InlineData("Gluten-Free Rolled Oats", true)]
    [InlineData("Gluten Free Pasta", true)]
    [InlineData("Whole Wheat Pasta", false)]
    public void IsGlutenFree_LabelOverridesIngredientTerms(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsGlutenFree);
    }

    [Theory]
    [InlineData("Vegan Cheese", true)]
    [InlineData("Vegan Butter", true)]
    [InlineData("Plant-Based Milk", true)]
    [InlineData("Cheddar Cheese", false)]
    public void IsVegan_LabelOverridesIngredientTerms(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsVegan);
    }

    [Theory]
    [InlineData("Lactose-Free Milk", true)]
    [InlineData("Dairy-Free Yogurt", true)]
    [InlineData("Lactose Free Cream", true)]
    [InlineData("Greek Yogurt", false)]
    public void IsLactoseFree_LabelOverridesIngredientTerms(string name, bool expected)
    {
        var ingredient = MakeIngredient(name);
        Assert.Equal(expected, ingredient.IsLactoseFree);
    }

    [Fact]
    public void IsVegetarian_VeganLabelOverridesIngredientTerms()
    {
        var ingredient = MakeIngredient("Vegan Chicken Strips");
        Assert.True(ingredient.IsVegetarian);
        Assert.True(ingredient.IsVegan);
    }

    [Fact]
    public void MatchesDietaryTags_EmptyTags_AlwaysPasses()
    {
        var food = CreateFood("Chicken Breast");
        Assert.True(Ingredient.MatchesDietaryTags(food, new HashSet<DietaryTag>()));
    }

    [Fact]
    public void MatchesDietaryTags_VeganExcludesMeat()
    {
        var food = CreateFood("Chicken Breast");
        var tags = new HashSet<DietaryTag> { DietaryTag.Vegan };

        Assert.False(Ingredient.MatchesDietaryTags(food, tags));
    }

    [Fact]
    public void MatchesDietaryTags_VeganExcludesDairy()
    {
        var food = CreateFood("Greek Yogurt");
        var tags = new HashSet<DietaryTag> { DietaryTag.Vegan };

        Assert.False(Ingredient.MatchesDietaryTags(food, tags));
    }

    [Fact]
    public void MatchesDietaryTags_VeganAllowsPlantBased()
    {
        var food = CreateFood("Tofu");
        var tags = new HashSet<DietaryTag> { DietaryTag.Vegan };

        Assert.True(Ingredient.MatchesDietaryTags(food, tags));
    }

    [Fact]
    public void MatchesDietaryTags_MultipleTags_AllMustPass()
    {
        var food = CreateFood("Whole Wheat Pasta");
        var tags = new HashSet<DietaryTag> { DietaryTag.Vegetarian, DietaryTag.GlutenFree };

        Assert.False(Ingredient.MatchesDietaryTags(food, tags));
    }

    [Fact]
    public void MatchesDietaryTags_GlutenFreeLabel_PassesGlutenFreeTag()
    {
        var food = CreateFood("Gluten-Free Bread");
        var tags = new HashSet<DietaryTag> { DietaryTag.GlutenFree };

        Assert.True(Ingredient.MatchesDietaryTags(food, tags));
    }

    [Fact]
    public void ScaledNutrients_CorrectFor150Grams()
    {
        var food = new FoodItem
        {
            Name = "Brown Rice",
            Calories = 112f,
            Protein = 2.6f,
            Carbs = 24f,
            Fats = 0.9f,
            Iron = 0.4f,
            VitaminC = 0f
        };

        var ingredient = new Ingredient(food, 150f);

        Assert.Equal(112f * 1.5f, ingredient.Calories, 0.01f);
        Assert.Equal(2.6f * 1.5f, ingredient.Protein, 0.01f);
        Assert.Equal(24f * 1.5f, ingredient.Carbs, 0.01f);
        Assert.Equal(0.9f * 1.5f, ingredient.Fats, 0.01f);
        Assert.Equal(0.4f * 1.5f, ingredient.Iron, 0.01f);
    }

    private static Ingredient MakeIngredient(string name, float grams = 100f)
    {
        return new Ingredient(CreateFood(name), grams);
    }

    private static FoodItem CreateFood(string name)
    {
        return new FoodItem
        {
            Id = 1,
            Name = name,
            Calories = 100f,
            Protein = 10f,
            Carbs = 10f,
            Fats = 5f
        };
    }
}

public class MealComponentDietaryTests
{
    [Fact]
    public void MatchesDietaryPreferences_AllVeganIngredients_PassesVeganTag()
    {
        var component = new MealComponent(MealConceptSlotType.VitaminBase, [
            new Ingredient(CreateFood("Spinach"), 80f),
            new Ingredient(CreateFood("Bell Pepper"), 60f),
            new Ingredient(CreateFood("Tomato"), 60f)
        ]);

        var tags = new HashSet<DietaryTag> { DietaryTag.Vegan };
        Assert.True(component.MatchesDietaryPreferences(tags));
    }

    [Fact]
    public void MatchesDietaryPreferences_OneMeatIngredient_FailsVegetarianTag()
    {
        var component = new MealComponent(MealConceptSlotType.ProteinBase, [
            new Ingredient(CreateFood("Chicken Breast"), 150f),
            new Ingredient(CreateFood("Onion"), 30f)
        ]);

        var tags = new HashSet<DietaryTag> { DietaryTag.Vegetarian };
        Assert.False(component.MatchesDietaryPreferences(tags));
    }

    [Fact]
    public void MatchesDietaryPreferences_OneDairyIngredient_FailsVeganTag()
    {
        var component = new MealComponent(MealConceptSlotType.Sauce, [
            new Ingredient(CreateFood("Olive Oil"), 10f),
            new Ingredient(CreateFood("Parmesan Cheese"), 5f)
        ]);

        var tags = new HashSet<DietaryTag> { DietaryTag.Vegan };
        Assert.False(component.MatchesDietaryPreferences(tags));
    }

    [Fact]
    public void MatchesDietaryPreferences_GlutenInCarb_FailsGlutenFreeTag()
    {
        var component = new MealComponent(MealConceptSlotType.CarbBase, [
            new Ingredient(CreateFood("Brown Rice"), 120f),
            new Ingredient(CreateFood("Flour Tortilla"), 50f)
        ]);

        var tags = new HashSet<DietaryTag> { DietaryTag.GlutenFree };
        Assert.False(component.MatchesDietaryPreferences(tags));
    }

    [Fact]
    public void MatchesDietaryPreferences_EmptyTags_AlwaysPasses()
    {
        var component = new MealComponent(MealConceptSlotType.ProteinBase, [
            new Ingredient(CreateFood("Chicken Breast"), 150f)
        ]);

        Assert.True(component.MatchesDietaryPreferences(new HashSet<DietaryTag>()));
    }

    [Fact]
    public void MatchesDietaryPreferences_LabelOverridesInComponent()
    {
        var component = new MealComponent(MealConceptSlotType.CarbBase, [
            new Ingredient(CreateFood("Gluten-Free Rolled Oats"), 80f),
            new Ingredient(CreateFood("Banana"), 100f)
        ]);

        var tags = new HashSet<DietaryTag> { DietaryTag.GlutenFree };
        Assert.True(component.MatchesDietaryPreferences(tags));
    }

    [Fact]
    public void AggregateMacros_SumsAllIngredients()
    {
        var rice = new FoodItem { Name = "Rice", Calories = 130f, Protein = 2.7f, Carbs = 28f, Fats = 0.3f };
        var lentils = new FoodItem { Name = "Lentils", Calories = 116f, Protein = 9f, Carbs = 20f, Fats = 0.4f };

        var component = new MealComponent(MealConceptSlotType.CarbBase, [
            new Ingredient(rice, 100f),
            new Ingredient(lentils, 50f)
        ]);

        var expected = 130f + 116f * 0.5f;
        Assert.Equal(expected, component.TotalCalories, 0.1f);
    }

    private static FoodItem CreateFood(string name)
    {
        return new FoodItem
        {
            Id = 1,
            Name = name,
            Calories = 100f,
            Protein = 10f,
            Carbs = 10f,
            Fats = 5f
        };
    }
}
