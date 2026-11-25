using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Overview;

/// <summary>Maps stored targets onto the DTOs, keeping unset targets absent rather than zero.</summary>
public static class NutritionTargetContractMapper
{
    /// <summary>Adds the per-meal distribution to the macro targets.</summary>
    public static NutritionTargetMacrosDto WithMealDistribution(
        NutritionTargetMacrosDto target,
        string? breakfastPreference)
    {
        ArgumentNullException.ThrowIfNull(target);
        var preference = Enum.TryParse<BreakfastPreference>(breakfastPreference, true, out var parsed) &&
                         Enum.IsDefined(parsed)
            ? parsed
            : BreakfastPreference.Savoury;
        var shares = MealDistributionRule.GetShares(preference);
        return target with
        {
            MealDistribution = new NutritionMealDistributionDto(
                shares.Breakfast, shares.Lunch, shares.Dinner, shares.Snack)
        };
    }
}
