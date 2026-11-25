namespace MuscleCuties.UI.Services.Localization;

/// <summary>Russian copy for the catalog's own keys, registered per feature and read by</summary>
/// <see cref="UiTextCatalog.Get" /> when the active language is Russian.</summary>
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

    /// <summary>Registers the language's strings the first time the type is used.</summary>
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

    /// <summary>Declares the dashboard string registration.</summary>
    static partial void RegisterDashboardStrings();
    /// <summary>Declares the cycle and workout string registration.</summary>
    static partial void RegisterCycleWorkoutStrings();
    /// <summary>Declares the nutrition, profile and auth string registration.</summary>
    static partial void RegisterNutritionProfileAuthStrings();
    /// <summary>Declares the workout generation string registration.</summary>
    static partial void RegisterWorkoutGenerationStrings();
    /// <summary>Declares the phase adjustment string registration.</summary>
    static partial void RegisterWorkoutPhaseAdjustmentStrings();
    /// <summary>Declares the hydration string registration.</summary>
    static partial void RegisterHydrationStrings();
    /// <summary>Declares the body measurement string registration.</summary>
    static partial void RegisterBodyMeasurementStrings();
}
