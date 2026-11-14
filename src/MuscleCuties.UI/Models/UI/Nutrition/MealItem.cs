using System.Collections.ObjectModel;
using MuscleCuties.UI.Services.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public partial class MealItem : ObservableObject
{
    public Guid HistoryId { get; set; }
    public int LoggedMealId { get; set; }
    public string Time { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CaloriesText { get; set; } = string.Empty;
    public string MacrosText { get; set; } = string.Empty;
    public string FiberText { get; set; } = string.Empty;
    public string NutrientSummaryText { get; set; } = string.Empty;
    public string IngredientsText { get; set; } = string.Empty;
    public NutritionMacros Macros { get; set; }
    public ObservableCollection<MacroBreakdownItem> MacroItems { get; set; } = new();
    public ObservableCollection<DailyMicronutrientItem> Micronutrients { get; set; } = new();

    [ObservableProperty] private string _targetShareText = string.Empty;

    public string MacrosSummaryText => NutritionFormat.Macros(Macros.Protein, Macros.Carbs, Macros.Fats);
    public float ProteinCalories => Macros.ProteinCalories;
    public float CarbsCalories => Macros.CarbsCalories;
    public float FatsCalories => Macros.FatsCalories;

    public string MealTypeIconData => MealTypeVisuals.GetIconData(MealType);
}
