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
        var safeWeight = float.IsFinite(weight) ? Math.Max(weight, 55f) : 55f;
        var minimumProtein = Math.Min(safeWeight * MinimumProteinPerKg(goal), calories * 0.45f / 4f);
        var minimumFats = Math.Min(Math.Max(safeWeight * 0.5f, calories * 0.18f / 9f), calories * 0.35f / 9f);
        var protein = Math.Max(calories * ratios.Protein / 4f, minimumProtein);
        var fats = Math.Max(calories * ratios.Fats / 9f, minimumFats);
        if (protein * 4f + fats * 9f > calories)
            fats = Math.Max(0f, Math.Min(fats, (calories - protein * 4f) / 9f));
        if (protein * 4f + fats * 9f > calories)
            protein = Math.Max(0f, (calories - fats * 9f) / 4f);
        var carbs = Math.Max((calories - protein * 4f - fats * 9f) / 4f, 0f);
        return new NutritionMacroTargets(
            RoundToNearest(protein, 1f), RoundToNearest(carbs, 1f),
            RoundToNearest(fats, 1f), FiberTarget(calories, phase));
    }

    public static NutritionCaloriesTargets? CalculateCalories(
        float protein, float carbs, float fats, CyclePhase phase)
    {
        if (!float.IsFinite(protein) || !float.IsFinite(carbs) || !float.IsFinite(fats) ||
            protein < 0f || carbs < 0f || fats < 0f)
            return null;
        var calories = RoundToNearest(protein * 4f + carbs * 4f + fats * 9f, 10f);
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
            var proteinShare = protein * 4f / calories;
            var carbsShare = carbs * 4f / calories;
            var fatsShare = fats * 9f / calories;
            var total = proteinShare + carbsShare + fatsShare;
            if (float.IsFinite(total) && total > 0f)
                return (proteinShare / total, carbsShare / total, fatsShare / total);
        }

        return goal switch
        {
            UserGoal.FatLoss => (0.34f, 0.36f, 0.30f),
            UserGoal.Strength => (0.25f, 0.50f, 0.25f),
            UserGoal.MuscleTone => (0.30f, 0.42f, 0.28f),
            _ => (0.25f, 0.45f, 0.30f)
        };
    }

    private static float MinimumProteinPerKg(UserGoal goal)
    {
        return goal switch
        {
            UserGoal.FatLoss => 1.8f,
            UserGoal.Strength => 1.7f,
            UserGoal.MuscleTone => 1.5f,
            _ => 1.2f
        };
    }

    private static float FiberTarget(float calories, CyclePhase phase)
    {
        return RoundToNearest(MathF.Max(25f, calories / 1000f * 14f) +
                              (phase is CyclePhase.Luteal ? 3f : 0f), 1f);
    }

    private static float RoundToNearest(float value, float nearest)
    {
        return nearest <= 0f ? value : MathF.Round(value / nearest) * nearest;
    }
}
