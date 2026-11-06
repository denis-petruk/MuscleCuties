namespace MuscleCuties.UI.Services.Localization;

internal static partial class RussianUiText
{
    internal static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        ["Tab_Today"] = "Сегодня",
        ["Tab_Cycle"] = "Цикл",
        ["Tab_Train"] = "Тренировки",
        ["Tab_Nutrition"] = "Питание",
        ["Tab_You"] = "Профиль",
        ["Progress_SelfWorthNote"] = "Используйте это для проверки прогресса, а не для доказательства своей ценности.",
        ["NutritionSettings_SaveFailed"] = "Настройки питания не сохранены. Пожалуйста, попробуйте снова.",
        ["Dashboard_HydrationValueFormat"] = "Гидратация: {0}",
        ["Nutrition_IngredientsGramsHeader"] = "Ингредиенты · граммы",
        ["Nutrition_MealTypeHeader"] = "Тип приема пищи",
    };

    static RussianUiText()
    {
        RegisterDashboardStrings();
        RegisterCycleWorkoutStrings();
        RegisterNutritionProfileAuthStrings();
        RegisterWorkoutGenerationStrings();
        RegisterWorkoutPhaseAdjustmentStrings();
        RegisterHydrationStrings();
        RegisterBodyMeasurementStrings();
    }

    static partial void RegisterDashboardStrings();
    static partial void RegisterCycleWorkoutStrings();
    static partial void RegisterNutritionProfileAuthStrings();
    static partial void RegisterWorkoutGenerationStrings();
    static partial void RegisterWorkoutPhaseAdjustmentStrings();
    static partial void RegisterHydrationStrings();
    static partial void RegisterBodyMeasurementStrings();
}
