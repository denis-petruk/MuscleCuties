using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record NutritionMacroTargets(float Protein, float Carbs, float Fats, float Fiber);

public sealed record NutritionCaloriesTargets(float Calories, float Fiber);

public static class ProfileNutritionTargetsRule
{
    public const float MinimumUserEnteredCalories = 1200f;
    public const float MaximumUserEnteredTargetValue = 5000f;

    private const float DefaultWeightKg = 55f;
    private const float ProteinShareOfCaloriesCap = 0.45f;
    private const float FatGramsPerKg = 0.5f;
    private const float FatShareOfCaloriesFloor = 0.18f;
    private const float FatShareOfCaloriesCap = 0.35f;

    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramCarb = 4f;
    private const float CaloriesPerGramFat = 9f;
    private const float MacroRoundingGrams = 1f;
    private const float CalorieRoundingKcal = 10f;

    private const float MinimumFiberGrams = 25f;
    private const float FiberGramsPerThousandKcal = 14f;
    private const float LutealFiberBonusGrams = 3f;
    private const float CaloriesPerThousand = 1000f;

    private const float FatLossProteinShare = 0.34f;
    private const float FatLossCarbShare = 0.36f;
    private const float FatLossFatShare = 0.30f;
    private const float FatLossProteinPerKg = 1.8f;

    private const float StrengthProteinShare = 0.25f;
    private const float StrengthCarbShare = 0.50f;
    private const float StrengthFatShare = 0.25f;
    private const float StrengthProteinPerKg = 1.7f;

    private const float MuscleToneProteinShare = 0.30f;
    private const float MuscleToneCarbShare = 0.42f;
    private const float MuscleToneFatShare = 0.28f;
    private const float MuscleToneProteinPerKg = 1.5f;

    private const float BalancedProteinShare = 0.25f;
    private const float BalancedCarbShare = 0.45f;
    private const float BalancedFatShare = 0.30f;
    private const float BalancedProteinPerKg = 1.2f;

    public static bool IsValidUserEnteredTarget(string key, float value)
    {
        if (!float.IsFinite(value) || value < 0f || value > MaximumUserEnteredTargetValue)
            return false;

        if (string.Equals(key, nameof(ProfileNutritionGoals.Calories), StringComparison.Ordinal))
            return value >= MinimumUserEnteredCalories;

        return key is nameof(ProfileNutritionGoals.Protein) or nameof(ProfileNutritionGoals.Carbs) or
            nameof(ProfileNutritionGoals.Fats) or nameof(ProfileNutritionGoals.Fiber) or
            nameof(ProfileNutritionGoals.WaterLiters) or nameof(ProfileNutritionGoals.Iron) or
            nameof(ProfileNutritionGoals.VitaminB12) or nameof(ProfileNutritionGoals.VitaminC) or
            nameof(ProfileNutritionGoals.VitaminD) or nameof(ProfileNutritionGoals.VitaminA) or
            nameof(ProfileNutritionGoals.VitaminB6) or nameof(ProfileNutritionGoals.Folate) or
            nameof(ProfileNutritionGoals.Calcium) or nameof(ProfileNutritionGoals.Magnesium) or
            nameof(ProfileNutritionGoals.Zinc) or nameof(ProfileNutritionGoals.Potassium);
    }

    public static NutritionMacroTargets? RebalanceMacros(
        ProfileNutritionGoals calculated, UserGoal goal, CyclePhase phase,
        float weight, float calories)
    {
        if (!float.IsFinite(calories) || calories < MinimumUserEnteredCalories || calories > MaximumUserEnteredTargetValue)
            return null;

        var ratios = MacroRatios(calculated, goal);
        var safeWeight = float.IsFinite(weight) ? Math.Max(weight, DefaultWeightKg) : DefaultWeightKg;
        var minimumProtein = Math.Min(safeWeight * MinimumProteinPerKg(goal),
            calories * ProteinShareOfCaloriesCap / CaloriesPerGramProtein);
        var minimumFats = Math.Min(
            Math.Max(safeWeight * FatGramsPerKg, calories * FatShareOfCaloriesFloor / CaloriesPerGramFat),
            calories * FatShareOfCaloriesCap / CaloriesPerGramFat);
        var protein = Math.Max(calories * ratios.Protein / CaloriesPerGramProtein, minimumProtein);
        var fats = Math.Max(calories * ratios.Fats / CaloriesPerGramFat, minimumFats);
        if (protein * CaloriesPerGramProtein + fats * CaloriesPerGramFat > calories)
            fats = Math.Max(0f, Math.Min(fats,
                (calories - protein * CaloriesPerGramProtein) / CaloriesPerGramFat));
        if (protein * CaloriesPerGramProtein + fats * CaloriesPerGramFat > calories)
            protein = Math.Max(0f, (calories - fats * CaloriesPerGramFat) / CaloriesPerGramProtein);
        var carbs = Math.Max(
            (calories - protein * CaloriesPerGramProtein - fats * CaloriesPerGramFat) / CaloriesPerGramCarb, 0f);
        return new NutritionMacroTargets(
            RoundToNearest(protein, MacroRoundingGrams), RoundToNearest(carbs, MacroRoundingGrams),
            RoundToNearest(fats, MacroRoundingGrams), FiberTarget(calories, phase));
    }

    public static NutritionCaloriesTargets? CalculateCalories(
        float protein, float carbs, float fats, CyclePhase phase)
    {
        if (!float.IsFinite(protein) || !float.IsFinite(carbs) || !float.IsFinite(fats) ||
            protein < 0f || carbs < 0f || fats < 0f)
            return null;
        var calories = RoundToNearest(
            protein * CaloriesPerGramProtein + carbs * CaloriesPerGramCarb + fats * CaloriesPerGramFat,
            CalorieRoundingKcal);
        return !float.IsFinite(calories) || calories < MinimumUserEnteredCalories || calories > MaximumUserEnteredTargetValue
            ? null
            : new NutritionCaloriesTargets(calories, FiberTarget(calories, phase));
    }

    private static (float Protein, float Carbs, float Fats) MacroRatios(
        ProfileNutritionGoals goals, UserGoal goal)
    {
        if (goals.Calories is > 0f and var calories &&
            goals.Protein is > 0f and var protein &&
            goals.Carbs is > 0f and var carbs &&
            goals.Fats is > 0f and var fats &&
            float.IsFinite(calories) && float.IsFinite(protein) &&
            float.IsFinite(carbs) && float.IsFinite(fats))
        {
            var proteinShare = protein * CaloriesPerGramProtein / calories;
            var carbsShare = carbs * CaloriesPerGramCarb / calories;
            var fatsShare = fats * CaloriesPerGramFat / calories;
            var total = proteinShare + carbsShare + fatsShare;
            if (float.IsFinite(total) && total > 0f)
                return (proteinShare / total, carbsShare / total, fatsShare / total);
        }

        return goal switch
        {
            UserGoal.FatLoss => (FatLossProteinShare, FatLossCarbShare, FatLossFatShare),
            UserGoal.Strength => (StrengthProteinShare, StrengthCarbShare, StrengthFatShare),
            UserGoal.MuscleTone => (MuscleToneProteinShare, MuscleToneCarbShare, MuscleToneFatShare),
            _ => (BalancedProteinShare, BalancedCarbShare, BalancedFatShare)
        };
    }

    private static float MinimumProteinPerKg(UserGoal goal)
    {
        return goal switch
        {
            UserGoal.FatLoss => FatLossProteinPerKg,
            UserGoal.Strength => StrengthProteinPerKg,
            UserGoal.MuscleTone => MuscleToneProteinPerKg,
            _ => BalancedProteinPerKg
        };
    }

    private static float FiberTarget(float calories, CyclePhase phase)
    {
        return RoundToNearest(
            MathF.Max(MinimumFiberGrams, calories / CaloriesPerThousand * FiberGramsPerThousandKcal) +
            (phase is CyclePhase.Luteal ? LutealFiberBonusGrams : 0f),
            MacroRoundingGrams);
    }

    private static float RoundToNearest(float value, float nearest)
    {
        return nearest <= 0f ? value : MathF.Round(value / nearest) * nearest;
    }
}
