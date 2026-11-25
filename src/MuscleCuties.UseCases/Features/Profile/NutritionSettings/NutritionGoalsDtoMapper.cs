using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

/// <summary>Maps stored goals onto the settings DTO and back.</summary>
public static class NutritionGoalsDtoMapper
{
    /// <summary>Maps stored goals onto the DTO, leaving unset targets absent rather than zero.</summary>
    public static NutritionGoalsDto ToDto(ProfileNutritionGoals goals)
    {
        return new NutritionGoalsDto(
            goals.Calories, goals.Protein, goals.Carbs, goals.Fats, goals.Fiber,
            goals.WaterLiters, goals.Iron, goals.VitaminB12, goals.VitaminC,
            goals.VitaminD, goals.VitaminA, goals.VitaminB6, goals.Folate,
            goals.Calcium, goals.Magnesium, goals.Zinc, goals.Potassium);
    }

    /// <summary>Maps the DTO back onto the stored goals, keeping her overrides only where she set them.</summary>
    public static ProfileNutritionGoals ToDomain(NutritionGoalsDto goals)
    {
        return new ProfileNutritionGoals(
            goals.Calories, goals.Protein, goals.Carbs, goals.Fats, goals.Fiber,
            goals.WaterLiters, goals.Iron, goals.VitaminB12, goals.VitaminC,
            goals.VitaminD, goals.VitaminA, goals.VitaminB6, goals.Folate,
            goals.Calcium, goals.Magnesium, goals.Zinc, goals.Potassium);
    }
}
