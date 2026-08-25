using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public sealed record NutritionPlan(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float Fiber,
    float WaterLiters,
    float Bmr,
    float Tdee,
    float ActivityMultiplier,
    float GoalAdjustment,
    float PhaseAdjustment,
    CyclePhase Phase,
    string PhaseFocus,
    IReadOnlyCollection<string> Notes,
    ProfileNutritionGoals Goals,
    IReadOnlyCollection<MealNutritionTarget> Meals);

public sealed record MealNutritionTarget(
    MealType MealType,
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
