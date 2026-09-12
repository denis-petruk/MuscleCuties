using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class MealComponentScorerTests
{
    private static readonly FoodItem Chicken = CreateFood(1, "Chicken Breast", 165f, 31f, 0f, 3.6f, iron: 1f);
    private static readonly FoodItem Salmon = CreateFood(2, "Salmon Fillet", 208f, 20f, 0f, 13f, iron: 0.8f);
    private static readonly FoodItem Tofu = CreateFood(3, "Tofu", 76f, 8f, 1.9f, 4.8f, iron: 5.4f, vitC: 0.1f);
    private static readonly FoodItem Rice = CreateFood(4, "Brown Rice", 112f, 2.6f, 24f, 0.9f);
    private static readonly FoodItem Quinoa = CreateFood(5, "Quinoa", 120f, 4.4f, 21f, 1.9f, magnesium: 64f);
    private static readonly FoodItem Broccoli = CreateFood(6, "Broccoli", 34f, 2.8f, 7f, 0.4f, vitC: 89.2f, iron: 0.7f);
    private static readonly FoodItem Spinach = CreateFood(7, "Spinach", 23f, 2.9f, 3.6f, 0.4f, iron: 2.7f, vitC: 28f, magnesium: 79f);
    private static readonly FoodItem OliveOil = CreateFood(8, "Olive Oil", 884f, 0f, 0f, 100f);

    private static readonly MealNutritionTarget LunchTarget = new(MealType.Lunch, 550f, 40f, 55f, 18f);
    private static readonly IReadOnlySet<DietaryTag> NoDiet = new HashSet<DietaryTag>();
    private static readonly IReadOnlyDictionary<int, int> NoUsage = new Dictionary<int, int>();

    [Fact]
    public void ScoreCombo_ValidCombo_ReturnsPositiveScore()
    {
        var score = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, OliveOil,
            LunchTarget, CyclePhase.Follicular, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(score > 0f);
    }

    [Fact]
    public void ScoreCombo_WithoutSauce_ReturnsPositiveScore()
    {
        var score = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(score > 0f);
    }

    [Fact]
    public void ScoreCombo_VeganDiet_ExcludesChicken()
    {
        var veganTags = new HashSet<DietaryTag> { DietaryTag.Vegan };

        var score = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, veganTags, NoUsage, UserGoal.MaintainHealth);

        Assert.Equal(-1f, score);
    }

    [Fact]
    public void ScoreCombo_VeganDiet_AllowsTofu()
    {
        var veganTags = new HashSet<DietaryTag> { DietaryTag.Vegan };

        var score = MealComponentScorer.ScoreCombo(
            Tofu, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, veganTags, NoUsage, UserGoal.MaintainHealth);

        Assert.True(score > 0f);
    }

    [Fact]
    public void ScoreCombo_VegetarianDiet_ExcludesChicken()
    {
        var vegTags = new HashSet<DietaryTag> { DietaryTag.Vegetarian };

        var score = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, vegTags, NoUsage, UserGoal.MaintainHealth);

        Assert.Equal(-1f, score);
    }

    [Fact]
    public void ScoreCombo_MenstrualPhase_FavorsIronRichFoods()
    {
        var ironRichScore = MealComponentScorer.ScoreCombo(
            Tofu, Rice, Spinach, null,
            LunchTarget, CyclePhase.Menstrual, NoDiet, NoUsage, UserGoal.MaintainHealth);

        var lowIronScore = MealComponentScorer.ScoreCombo(
            Salmon, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Menstrual, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(ironRichScore > lowIronScore);
    }

    [Fact]
    public void ScoreCombo_LutealPhase_FavorsMagnesiumRichFoods()
    {
        var magRichScore = MealComponentScorer.ScoreCombo(
            Chicken, Quinoa, Spinach, null,
            LunchTarget, CyclePhase.Luteal, NoDiet, NoUsage, UserGoal.MaintainHealth);

        var lowMagScore = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Luteal, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(magRichScore > lowMagScore);
    }

    [Fact]
    public void ScoreCombo_RecentlyUsedFoods_ScoreLower()
    {
        var recentUsage = new Dictionary<int, int>
        {
            { Chicken.Id, 1 },
            { Rice.Id, 0 },
            { Broccoli.Id, 1 }
        };

        var recentScore = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, NoDiet, recentUsage, UserGoal.MaintainHealth);

        var freshScore = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(freshScore > recentScore);
    }

    [Fact]
    public void ScoreCombo_MaxScoreIsBounded()
    {
        var score = MealComponentScorer.ScoreCombo(
            Chicken, Rice, Broccoli, OliveOil,
            LunchTarget, CyclePhase.Follicular, NoDiet, NoUsage, UserGoal.MaintainHealth);

        Assert.True(score <= 105f);
    }

    [Fact]
    public void ScoreCombo_GlutenFree_ExcludesWheatPasta()
    {
        var pasta = CreateFood(20, "Whole Wheat Pasta", 131f, 5.3f, 27f, 0.6f);
        var gfTags = new HashSet<DietaryTag> { DietaryTag.GlutenFree };

        var score = MealComponentScorer.ScoreCombo(
            Chicken, pasta, Broccoli, null,
            LunchTarget, CyclePhase.Follicular, gfTags, NoUsage, UserGoal.MaintainHealth);

        Assert.Equal(-1f, score);
    }

    private static FoodItem CreateFood(
        int id, string name, float cal, float protein, float carbs, float fats,
        float iron = 0f, float vitC = 0f, float magnesium = 0f, float vitB6 = 0f,
        float folate = 0f, float zinc = 0f, float calcium = 0f, float vitB12 = 0f)
    {
        return new FoodItem
        {
            Id = id,
            Name = name,
            Calories = cal,
            Protein = protein,
            Carbs = carbs,
            Fats = fats,
            Iron = iron,
            VitaminC = vitC,
            Magnesium = magnesium,
            VitaminB6 = vitB6,
            Folate = folate,
            Zinc = zinc,
            Calcium = calcium,
            VitaminB12 = vitB12
        };
    }
}
