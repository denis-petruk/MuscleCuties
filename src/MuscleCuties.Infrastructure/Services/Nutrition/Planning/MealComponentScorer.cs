using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public static class MealComponentScorer
{
    private const float IneligibleScore = -1f;
    private const int SingleTermThreshold = 1;
    private const int StyleTermThreshold = 2;
    private const float StylePrimaryBonus = 3f;
    private const float StyleSecondaryBonus = 2f;
    private const float StyleSupportingBonus = 1f;
    private const float PhaseScoreWeight = 30f;
    private const float DefaultPhaseScore = 15f;
    private const float MagnesiumWeight = 0.5f;
    private const float FolateWeight = 0.01f;
    private const float IronWeight = 0.01f;
    private const float NutrientDensityDivisor = 20f;
    private const float VarietyTotalScore = 25f;
    private const float VarietyFullCreditDays = 7f;
    private const float MacroFallbackScore = 12.5f;
    private const float ProteinDensityScale = 100f;
    private const float ProteinDensityTarget = 30f;
    private const float FatLossDensityThreshold = 20f;
    private const float FatLossDensityBonus = 0.2f;
    private const float MacroFitTotalScore = 25f;
    private const float CuisineBaseScore = 10f;
    private const float CuisineMaxScore = 20f;
    private const int CuisineMatchThreshold = 2;

    private static readonly string[] LatinTerms =
        ["quinoa", "bean", "avocado", "tortilla", "rice", "salsa"];

    private static readonly string[] AsianTerms =
        ["tofu", "soy", "edamame", "rice", "noodle", "sesame", "ginger"];

    private static readonly string[] MediterraneanTerms =
        ["olive", "feta", "hummus", "couscous", "chickpea", "lentil"];

    private static readonly string[] BowlTerms =
        ["rice", "quinoa", "noodle", "couscous", "grain", "oat", "bean", "lentil"];

    private static readonly string[] StirFryTerms =
        ["tofu", "soy", "sesame", "ginger", "teriyaki", "stir"];

    private static readonly string[] CurryTerms =
        ["curry", "coconut", "lentil", "chickpea", "turmeric"];

    private static readonly string[] PlatedProteinTerms =
        ["chicken", "salmon", "tuna", "turkey", "beef", "shrimp", "cod", "pork"];

    private static readonly string[] HandheldTerms =
        ["bun", "hamburger", "bread", "toast", "baguette", "dinner roll", "bagel", "muffin"];

    private static readonly string[] CookingSauceTerms =
        ["crushed tomato", "tomato sauce", "broth", "stew", "curry paste", "coconut milk"];

    private static readonly string[] WrappedTerms =
        ["tortilla", "wrap", "pita", "flatbread", "lavash", "naan"];

    public static MealStyle DetermineStyle(
        FoodItem protein, FoodItem carb, FoodItem veg, FoodItem? sauce)
    {
        // Vessel-based detection takes priority: the carb base defines
        // whether the meal is held in hand or wrapped, regardless of
        // what the filling ingredients are named.
        // Check wrapped first — "pita bread" should be Wrapped, not Handheld.
        if (ContainsAny(carb.Name, WrappedTerms))
            return MealStyle.Wrapped;
        if (ContainsAny(carb.Name, HandheldTerms))
        {
            // Bread paired with a cooking sauce (e.g. Shakshuka) is a
            // Complex dish, not a handheld sandwich/burger.
            if (sauce is not null && ContainsAny(sauce.Name, CookingSauceTerms))
                return MealStyle.Complex;
            return MealStyle.Handheld;
        }

        var names = sauce is not null
            ? new[] { protein.Name, carb.Name, veg.Name, sauce.Name }
            : new[] { protein.Name, carb.Name, veg.Name };

        var hasSauce = sauce is not null;
        var bowlScore = names.Count(n => ContainsAny(n, BowlTerms))
                        + names.Count(n => ContainsAny(n, AsianTerms));
        var complexScore = names.Count(n => ContainsAny(n, StirFryTerms))
                           + names.Count(n => ContainsAny(n, CurryTerms));

        if (hasSauce && complexScore >= StyleTermThreshold)
            return MealStyle.Complex;
        if (bowlScore >= StyleTermThreshold)
            return MealStyle.Bowl;
        if (hasSauce && bowlScore >= SingleTermThreshold)
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
            return IneligibleScore;

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
                if (ContainsAny(carb.Name, BowlTerms)) bonus += StylePrimaryBonus;
                if (names.Any(n => ContainsAny(n, AsianTerms))) bonus += StyleSecondaryBonus;
                break;
            case MealStyle.Complex:
                if (sauce is not null) bonus += StylePrimaryBonus;
                if (names.Any(n => ContainsAny(n, StirFryTerms) || ContainsAny(n, CurryTerms)))
                    bonus += StyleSecondaryBonus;
                break;
            case MealStyle.Handheld:
                if (ContainsAny(carb.Name, HandheldTerms)) bonus += StylePrimaryBonus;
                if (ContainsAny(protein.Name, PlatedProteinTerms)) bonus += StyleSecondaryBonus;
                break;
            case MealStyle.Wrapped:
                if (ContainsAny(carb.Name, WrappedTerms)) bonus += StylePrimaryBonus;
                if (names.Any(n => ContainsAny(n, LatinTerms))) bonus += StyleSecondaryBonus;
                break;
            case MealStyle.Plated:
                if (ContainsAny(protein.Name, PlatedProteinTerms)) bonus += StyleSecondaryBonus;
                if (names.Any(n => ContainsAny(n, MediterraneanTerms))) bonus += StyleSupportingBonus;
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
            CyclePhase.Menstrual => PhaseScoreWeight * NutrientDensity(
                foods, f => f.Iron + f.VitaminC + f.Magnesium * MagnesiumWeight),
            CyclePhase.Follicular => PhaseScoreWeight * NutrientDensity(
                foods, f => f.VitaminB6 + f.Folate * FolateWeight),
            CyclePhase.Ovulatory => PhaseScoreWeight * NutrientDensity(foods, f => f.Zinc + f.VitaminB12),
            CyclePhase.Luteal => PhaseScoreWeight * NutrientDensity(
                foods, f => f.Magnesium + f.Calcium + f.Iron * IronWeight),
            _ => DefaultPhaseScore
        };
    }

    private static float NutrientDensity(FoodItem[] foods, Func<FoodItem, float> selector)
    {
        var total = foods.Sum(selector);
        return Math.Clamp(total / NutrientDensityDivisor, 0f, 1f);
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
        return VarietyTotalScore * (score / count);
    }

    private static float VarietyScore(int foodId, IReadOnlyDictionary<int, int> daysSinceUsed)
    {
        if (!daysSinceUsed.TryGetValue(foodId, out var days))
            return 1f;

        return Math.Clamp(days / VarietyFullCreditDays, 0f, 1f);
    }

    private static float ScoreMacroFitPotential(
        FoodItem protein, MealNutritionTarget target, UserGoal goal)
    {
        if (target.Protein <= 0f)
            return MacroFallbackScore;

        var densityRatio = protein.Protein / Math.Max(protein.Calories, 1f) * ProteinDensityScale;
        var densityScore = Math.Clamp(densityRatio / ProteinDensityTarget, 0f, 1f);

        var goalBonus = goal == UserGoal.FatLoss && densityRatio > FatLossDensityThreshold
            ? FatLossDensityBonus
            : 0f;

        return MacroFitTotalScore * Math.Clamp(densityScore + goalBonus, 0f, 1f);
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
        if (maxMatch < CuisineMatchThreshold)
            return CuisineBaseScore;

        return CuisineMaxScore * Math.Clamp(maxMatch / (float)names.Length, 0f, 1f);
    }

    private static bool ContainsAny(string value, string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
