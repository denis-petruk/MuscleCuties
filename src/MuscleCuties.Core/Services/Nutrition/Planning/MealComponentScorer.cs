using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class MealComponentScorer
{
    private static readonly string[] LatinTerms =
        ["quinoa", "bean", "avocado", "tortilla", "rice", "salsa"];

    private static readonly string[] AsianTerms =
        ["tofu", "soy", "edamame", "rice", "noodle", "sesame", "ginger"];

    private static readonly string[] MediterraneanTerms =
        ["olive", "feta", "hummus", "couscous", "chickpea", "lentil"];

    private static readonly string[] BowlTerms =
        ["rice", "quinoa", "noodle", "couscous", "grain"];

    private static readonly string[] StirFryTerms =
        ["tofu", "soy", "sesame", "ginger", "teriyaki", "stir"];

    private static readonly string[] CurryTerms =
        ["curry", "coconut", "lentil", "chickpea", "turmeric"];

    private static readonly string[] PlatedProteinTerms =
        ["chicken", "salmon", "tuna", "turkey", "beef", "shrimp", "cod", "pork"];

    public static MealStyle DetermineStyle(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce)
    {
        var names = sauce is not null
            ? new[] { protein.Name, carb.Name, veg.Name, sauce.Name }
            : new[] { protein.Name, carb.Name, veg.Name };

        var hasSauce = sauce is not null;
        var bowlScore = names.Count(n => ContainsAny(n, BowlTerms))
                      + names.Count(n => ContainsAny(n, AsianTerms));
        var complexScore = names.Count(n => ContainsAny(n, StirFryTerms))
                         + names.Count(n => ContainsAny(n, CurryTerms));

        if (hasSauce && complexScore >= 2)
            return MealStyle.Complex;
        if (bowlScore >= 2)
            return MealStyle.Bowl;
        if (hasSauce && bowlScore >= 1)
            return MealStyle.Bowl;

        return MealStyle.Plated;
    }

    public static float ScoreCombo(
        FoodItem protein,
        FoodItem carb,
        FoodItem veg,
        FoodItem? sauce,
        MealNutritionTarget target,
        CyclePhase phase,
        IReadOnlySet<DietaryTag> dietaryTags,
        IReadOnlyDictionary<int, int> daysSinceUsed,
        UserGoal goal,
        MealStyle style = MealStyle.Plated)
    {
        if (!FoodComponentClassifier.IsProteinAnchor(protein) ||
            !PassesDietaryFilter(protein, dietaryTags) ||
            !PassesDietaryFilter(carb, dietaryTags) ||
            !PassesDietaryFilter(veg, dietaryTags) ||
            (sauce is not null && !PassesDietaryFilter(sauce, dietaryTags)))
            return -1f;

        return ScorePhaseMicronutrients(protein, carb, veg, sauce, phase) +
               ScoreVariety(protein, carb, veg, sauce, daysSinceUsed) +
               ScoreMacroFitPotential(protein, target, goal) +
               ScoreCuisineCoherence(protein, carb, veg, sauce) +
               ScoreStyleFit(protein, carb, veg, sauce, style);
    }

    public static bool PassesDietaryFilter(FoodItem food, IReadOnlySet<DietaryTag> tags)
    {
        return Ingredient.MatchesDietaryTags(food, tags);
    }

    private static float ScoreStyleFit(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce,
        MealStyle style)
    {
        var names = sauce is not null
            ? new[] { protein.Name, carb.Name, veg.Name, sauce.Name }
            : new[] { protein.Name, carb.Name, veg.Name };

        var bonus = 0f;

        switch (style)
        {
            case MealStyle.Bowl:
                if (ContainsAny(carb.Name, BowlTerms)) bonus += 3f;
                if (names.Any(n => ContainsAny(n, AsianTerms))) bonus += 2f;
                break;
            case MealStyle.Complex:
                if (sauce is not null) bonus += 3f;
                if (names.Any(n => ContainsAny(n, StirFryTerms) || ContainsAny(n, CurryTerms)))
                    bonus += 2f;
                break;
            case MealStyle.Plated:
                if (ContainsAny(protein.Name, PlatedProteinTerms)) bonus += 2f;
                if (names.Any(n => ContainsAny(n, MediterraneanTerms))) bonus += 1f;
                break;
        }

        return bonus;
    }

    private static float ScorePhaseMicronutrients(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce,
        CyclePhase phase)
    {
        var foods = sauce is not null
            ? new[] { protein, carb, veg, sauce }
            : new[] { protein, carb, veg };

        return phase switch
        {
            CyclePhase.Menstrual => 30f * NutrientDensity(foods, f => f.Iron + f.VitaminC * 0.5f),
            CyclePhase.Follicular => 30f * NutrientDensity(foods, f => f.VitaminB6 + f.Folate * 0.01f),
            CyclePhase.Ovulatory => 30f * NutrientDensity(foods, f => f.Zinc + f.VitaminB12),
            CyclePhase.Luteal => 30f * NutrientDensity(foods, f => f.Magnesium + f.Calcium * 0.01f),
            _ => 15f
        };
    }

    private static float NutrientDensity(FoodItem[] foods, Func<FoodItem, float> selector)
    {
        var total = foods.Sum(selector);
        return Math.Clamp(total / 20f, 0f, 1f);
    }

    private static float ScoreVariety(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce,
        IReadOnlyDictionary<int, int> daysSinceUsed)
    {
        var score = VarietyScore(protein.Id, daysSinceUsed) +
                    VarietyScore(carb.Id, daysSinceUsed) +
                    VarietyScore(veg.Id, daysSinceUsed);

        if (sauce is not null)
            score += VarietyScore(sauce.Id, daysSinceUsed);

        var count = sauce is not null ? 4f : 3f;
        return 25f * (score / count);
    }

    private static float VarietyScore(int foodId, IReadOnlyDictionary<int, int> daysSinceUsed)
    {
        if (!daysSinceUsed.TryGetValue(foodId, out var days))
            return 1f;

        return Math.Clamp(days / 7f, 0f, 1f);
    }

    private static float ScoreMacroFitPotential(
        FoodItem protein, MealNutritionTarget target, UserGoal goal)
    {
        if (target.Protein <= 0f)
            return 12.5f;

        var densityRatio = protein.Protein / Math.Max(protein.Calories, 1f) * 100f;
        var densityScore = Math.Clamp(densityRatio / 30f, 0f, 1f);

        var goalBonus = goal == UserGoal.FatLoss && densityRatio > 20f ? 0.2f : 0f;

        return 25f * Math.Clamp(densityScore + goalBonus, 0f, 1f);
    }

    private static float ScoreCuisineCoherence(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce)
    {
        var names = sauce is not null
            ? new[] { protein.Name, carb.Name, veg.Name, sauce.Name }
            : new[] { protein.Name, carb.Name, veg.Name };

        var latinCount = names.Count(n => ContainsAny(n, LatinTerms));
        var asianCount = names.Count(n => ContainsAny(n, AsianTerms));
        var medCount = names.Count(n => ContainsAny(n, MediterraneanTerms));

        var maxMatch = Math.Max(latinCount, Math.Max(asianCount, medCount));
        if (maxMatch < 2)
            return 10f;

        return 20f * Math.Clamp(maxMatch / (float)names.Length, 0f, 1f);
    }

    private static bool ContainsAny(string value, string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
