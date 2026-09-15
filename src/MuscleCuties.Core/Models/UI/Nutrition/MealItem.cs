using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MealItem
{
    public int LoggedMealId { get; set; }
    public string Time { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CaloriesText { get; set; } = string.Empty;
    public string MacrosText { get; set; } = string.Empty;
    public string FiberText { get; set; } = string.Empty;
    public string NutrientSummaryText { get; set; } = string.Empty;
    public MacroNutrients Macros { get; set; }
    public ObservableCollection<MacroBreakdownItem> MacroItems { get; set; } = new();
    public ObservableCollection<DailyMicronutrientItem> Micronutrients { get; set; } = new();

    public string MacrosSummaryText => $"P {Macros.Protein:N0}g · C {Macros.Carbs:N0}g · F {Macros.Fats:N0}g";
    public float ProteinCalories => Macros.Protein * 4f;
    public float CarbsCalories => Macros.Carbs * 4f;
    public float FatsCalories => Macros.Fats * 9f;

    public string MealTypeIconData => MealType switch
    {
        "BREAKFAST" => "M17 18a5 5 0 0 0 0-10H5a5 5 0 0 0 0 10zM2 21h20M12 3v2M4.22 5.22l1.42 1.42M19.78 5.22l-1.42 1.42",
        "LUNCH" => "M12 2a8.5 8.5 0 0 1 8.5 8.5c0 3.7-2.4 6.8-5.7 8H9.2c-3.3-1.2-5.7-4.3-5.7-8A8.5 8.5 0 0 1 12 2zM12 2v4M4.93 10h14.14",
        "DINNER" => "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z",
        "SNACK" => "M12 2a10 10 0 0 0-6.88 2.77A4 4 0 0 1 8 8a4 4 0 0 1-3.54 3.97A10 10 0 1 0 12 2zM10 9a1 1 0 1 1-2 0 1 1 0 0 1 2 0zM15 13a1 1 0 1 1-2 0 1 1 0 0 1 2 0zM10 16a1 1 0 1 1-2 0 1 1 0 0 1 2 0zM14 9a1 1 0 1 1-2 0 1 1 0 0 1 2 0z",
        _ => "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7V7z"
    };
}
