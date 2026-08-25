using System.Collections.ObjectModel;
using MuscleCuties.Core.Services.Nutrition;

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

    public float ProteinCalories => Macros.Protein * 4f;
    public float CarbsCalories => Macros.Carbs * 4f;
    public float FatsCalories => Macros.Fats * 9f;
}
