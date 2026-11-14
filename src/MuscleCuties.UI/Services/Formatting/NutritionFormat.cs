using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Services.Formatting;

/// <summary>
///     The single place user-visible quantities are rendered (D3, D10, D26, D28, D38, D43): one decimal
///     separator from the current culture (a comma in Russian), one unit suffix from the catalogue, and
///     no hand-built "g"/"kcal" literals anywhere else in the UI.
/// </summary>
public static class NutritionFormat
{
    public static string Grams(double grams) => UiTextCatalog.Format("Nutrition_Format_Grams", grams);

    public static string GramsShort(double grams) => UiTextCatalog.Format("Nutrition_Format_GramsShort", grams);

    public static string Milligrams(double milligrams) => UiTextCatalog.Format("Nutrition_Format_Milligrams", milligrams);

    public static string Micrograms(double micrograms) => UiTextCatalog.Format("Nutrition_Format_Micrograms", micrograms);

    public static string Calories(double kilocalories) => UiTextCatalog.Format("Nutrition_Format_Calories", kilocalories);

    public static string Micronutrient(double amount, string unit) =>
        UiTextCatalog.Format("Nutrition_Format_QuantityWithUnit", amount, UnitLabel(unit));

    private static string UnitLabel(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "mg" or "мг" => UiTextCatalog.Get("Nutrition_Unit_Milligrams"),
        "mcg" or "µg" or "ug" or "мкг" => UiTextCatalog.Get("Nutrition_Unit_Micrograms"),
        "g" or "г" => UiTextCatalog.Get("Nutrition_Unit_Grams"),
        _ => unit
    };

    public static string Macros(double protein, double carbs, double fats) =>
        UiTextCatalog.Format("Nutrition_Format_Macros", protein, carbs, fats);
}
