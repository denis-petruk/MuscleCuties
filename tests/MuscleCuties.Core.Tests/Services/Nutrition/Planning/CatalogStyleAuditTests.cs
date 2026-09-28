using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services.Nutrition.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests.Services.Nutrition.Planning;

/// <summary>
/// Audits every catalog concept's primary carb against DetermineStyle
/// to verify every meal type resolves to a logical presentation format.
/// </summary>
public class CatalogStyleAuditTests
{
    private static FoodItem F(string name, float protein = 5f, float carbs = 20f, float fats = 2f)
        => new()
        {
            Id = 1, Name = name,
            Calories = protein * 4f + carbs * 4f + fats * 9f,
            Protein = protein, Carbs = carbs, Fats = fats
        };

    // ── Sweet Breakfast ─────────────────────────────────────────

    [Theory]
    [InlineData("Rolled Oats")]
    [InlineData("Oats")]
    [InlineData("Gluten-Free Rolled Oats")]
    public void OatBowl_Returns_Bowl(string carbName)
    {
        var result = MealComponentScorer.DetermineStyle(
            F("Greek Yogurt", protein: 10f), F(carbName), F("Blueberries"), F("Honey", fats: 0f));

        Assert.Equal(MealStyle.Bowl, result);
    }

    [Fact]
    public void YogurtParfait_Oat_Carb_Returns_Bowl()
    {
        var result = MealComponentScorer.DetermineStyle(
            F("Greek Yogurt", protein: 10f), F("Oats"), F("Strawberries"), F("Honey", fats: 0f));

        Assert.Equal(MealStyle.Bowl, result);
    }

    // ── Savory Breakfast ────────────────────────────────────────

    [Fact]
    public void Shakshuka_Bread_With_Tomato_Sauce_Returns_Complex()
    {
        var result = MealComponentScorer.DetermineStyle(
            F("Eggs", protein: 13f), F("Whole Wheat Bread"),
            F("Bell Pepper"), F("Crushed Tomato", fats: 0.5f));

        Assert.Equal(MealStyle.Complex, result);
    }

    // ── Dinner ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Black Beans")]
    [InlineData("Pinto Beans")]
    [InlineData("Cannellini Beans")]
    public void BeanStewBowl_Returns_Bowl(string carbName)
    {
        var result = MealComponentScorer.DetermineStyle(
            F("Chicken", protein: 31f), F(carbName),
            F("Tomato"), F("Crushed Tomato", fats: 0.5f));

        Assert.Equal(MealStyle.Bowl, result);
    }

    [Fact]
    public void LentilPowerBowl_Returns_Bowl()
    {
        var result = MealComponentScorer.DetermineStyle(
            F("Eggs", protein: 13f), F("Lentils"),
            F("Kale"), F("Olive Oil", protein: 0f, fats: 14f));

        Assert.Equal(MealStyle.Bowl, result);
    }

    // ── Snack ───────────────────────────────────────────────────

    [Fact]
    public void YogurtBowl_Berry_Carb_Uses_PreferredStyle()
    {
        // Berry carb can't be auto-detected as Bowl by DetermineStyle,
        // so the Yogurt Bowl concept uses PreferredStyle = Bowl.
        var yogurtBowlConcepts = SavouryMealCatalog.GetConcepts(MealType.Snack);
        var yogurtBowl = yogurtBowlConcepts.FirstOrDefault(c => c.Name == "Yogurt Bowl");

        Assert.NotNull(yogurtBowl);
        Assert.Equal(MealStyle.Bowl, yogurtBowl.PreferredStyle);
    }

    // ── Null-format safety ──────────────────────────────────────

    [Fact]
    public void PreferredStyle_Null_Falls_Through_To_DetermineStyle()
    {
        var concept = new MealConcept(
            "Test Concept", "Test", [MealType.Lunch], [], [],
            new HashSet<DietaryTag>(), null);

        // When PreferredStyle is null, the service uses DetermineStyle.
        // Verify null coalescing works correctly.
        var style = concept.PreferredStyle
            ?? MealComponentScorer.DetermineStyle(
                F("Chicken", protein: 31f),
                F("Brown Rice"),
                F("Spinach"),
                null);

        Assert.Equal(MealStyle.Bowl, style);
    }
}
