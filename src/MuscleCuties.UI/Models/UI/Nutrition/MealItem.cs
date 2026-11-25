
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>A logged meal as the nutrition screen lists it, with its breakdowns precomputed for the card.</summary>
public partial class MealItem : ObservableObject
{
    /// <summary>History entry that opens this meal's detail.</summary>
    public Guid HistoryId { get; set; }
    /// <summary>Logged meal behind the entry; zero when the meal was logged as loose foods.</summary>
    public int LoggedMealId { get; set; }
    /// <summary>Time the meal was logged, already formatted.</summary>
    public string Time { get; set; } = string.Empty;
    /// <summary>Meal type name, which also selects the icon.</summary>
    public string MealType { get; set; } = string.Empty;
    /// <summary>Meal or food name shown as the card title.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Calorie total, formatted.</summary>
    public string CaloriesText { get; set; } = string.Empty;
    /// <summary>Macro line, formatted.</summary>
    public string MacrosText { get; set; } = string.Empty;
    /// <summary>Fibre total, shown when the food reports it.</summary>
    public string FiberText { get; set; } = string.Empty;
    /// <summary>Short micronutrient summary under the macros.</summary>
    public string NutrientSummaryText { get; set; } = string.Empty;
    /// <summary>Ingredient list for a grouped meal, truncated to the display limit.</summary>
    public string IngredientsText { get; set; } = string.Empty;
    /// <summary>The meal's macro values, which the card and the breakdown are both derived from.</summary>
    public NutritionMacros Macros { get; set; }
    /// <summary>Per-macro slices of the meal's calories.</summary>
    public ObservableCollection<MacroBreakdownItem> MacroItems { get; set; } = new();
    /// <summary>Micronutrients the meal contributes, with progress against their daily goals.</summary>
    public ObservableCollection<DailyMicronutrientItem> Micronutrients { get; set; } = new();

    [ObservableProperty] private string _targetShareText = string.Empty;

    /// <summary>Macro line derived from the stored values rather than the preformatted text.</summary>
    public string MacrosSummaryText => DisplayFormat.Macros(Macros.Protein, Macros.Carbs, Macros.Fats);
    /// <summary>The calories the protein contributes.</summary>
    public float ProteinCalories => Macros.ProteinCalories;
    /// <summary>The calories the carbs contribute.</summary>
    public float CarbsCalories => Macros.CarbsCalories;
    /// <summary>The calories the fats contribute.</summary>
    public float FatsCalories => Macros.FatsCalories;

    /// <summary>Outline glyph for the meal's type.</summary>
    public string MealTypeIconData => MealTypeVisuals.GetIconData(MealType);
}
