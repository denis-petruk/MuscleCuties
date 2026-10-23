using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

public static class NutritionGoalsDtoMapper
{
    public static NutritionGoalsDto ToDto(ProfileNutritionGoals goals)
    {
        return new NutritionGoalsDto(
            goals.Calories, goals.Protein, goals.Carbs, goals.Fats, goals.Fiber,
            goals.WaterLiters, goals.Iron, goals.VitaminB12, goals.VitaminC,
            goals.VitaminD, goals.VitaminA, goals.VitaminB6, goals.Folate,
            goals.Calcium, goals.Magnesium, goals.Zinc, goals.Potassium);
    }

    public static ProfileNutritionGoals ToDomain(NutritionGoalsDto goals)
    {
        return new ProfileNutritionGoals(
            goals.Calories, goals.Protein, goals.Carbs, goals.Fats, goals.Fiber,
            goals.WaterLiters, goals.Iron, goals.VitaminB12, goals.VitaminC,
            goals.VitaminD, goals.VitaminA, goals.VitaminB6, goals.Folate,
            goals.Calcium, goals.Magnesium, goals.Zinc, goals.Potassium);
    }
}
