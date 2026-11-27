using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

/// <summary>A day's nutrition plan, with the targets it must meet.</summary>
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
    IReadOnlyCollection<MealNutritionTarget> Meals,
    BreakfastPreference BreakfastPreference = BreakfastPreference.Savoury,
    NutritionTargetProvenance? TargetProvenance = null);

/// <summary>The targets one meal must hit within the day's plan.</summary>
public sealed record MealNutritionTarget(
    MealType MealType,
    float Calories,
    float Protein,
    float Carbs,
    float Fats);
