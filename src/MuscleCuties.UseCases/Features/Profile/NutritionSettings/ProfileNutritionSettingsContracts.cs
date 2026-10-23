using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

public sealed record NutritionSettingsProfileDto(
    DateTime DateOfBirth,
    float Height,
    float Weight,
    TrainingExperienceLevel TrainingExperienceLevel,
    int WorkoutDaysPerWeek,
    UserGoal Goal,
    WeightGoalPace WeightGoalPace,
    string DietaryTags,
    NutritionGoalsDto SavedGoals,
    CyclePhase Phase);

public sealed record NutritionSettingsSelectionDto(
    UserGoal Goal,
    WeightGoalPace WeightGoalPace,
    string DietaryTags);

public sealed record NutritionSettingsSaveDto(
    NutritionSettingsProfileDto Profile,
    NutritionSettingsSelectionDto Selection,
    NutritionGoalsDto? EditedGoals);

public sealed record NutritionMacroTargetsDto(float Protein, float Carbs, float Fats, float Fiber);

public sealed record NutritionCaloriesTargetsDto(float Calories, float Fiber);

public interface IProfileNutritionSettingsUseCase
{
    Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken = default);
    NutritionGoalsDto CalculateGoals(NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection);
    NutritionGoalsDto MergeGoals(NutritionGoalsDto saved, NutritionGoalsDto calculated);
    bool HasOverrides(NutritionGoalsDto edited, NutritionGoalsDto calculated);

    NutritionMacroTargetsDto? RebalanceMacros(NutritionSettingsProfileDto profile,
        NutritionSettingsSelectionDto selection, float calories);

    NutritionCaloriesTargetsDto? CalculateCalories(NutritionSettingsProfileDto profile,
        float protein, float carbs, float fats);

    Task<bool> SaveAsync(NutritionSettingsSaveDto request, CancellationToken cancellationToken = default);
}

public sealed record NutritionGoalsDto(
    float? Calories,
    float? Protein,
    float? Carbs,
    float? Fats,
    float? Fiber,
    float? WaterLiters,
    float? Iron,
    float? VitaminB12,
    float? VitaminC,
    float? VitaminD,
    float? VitaminA,
    float? VitaminB6,
    float? Folate,
    float? Calcium,
    float? Magnesium,
    float? Zinc,
    float? Potassium)
{
    public static NutritionGoalsDto Empty { get; } = new(
        null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null);
}
