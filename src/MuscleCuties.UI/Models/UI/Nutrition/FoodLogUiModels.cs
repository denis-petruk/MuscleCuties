using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public partial class DateStripDayItem : ObservableObject
{
    [ObservableProperty] private bool _hasData;

    [ObservableProperty] private bool _isSelected;

    public DateStripDayItem(DateOnly date, bool isToday)
    {
        Date = date;
        DayName = date.ToString("ddd", CultureInfo.CurrentCulture).ToUpperInvariant();
        DayNumber = date.Day.ToString(CultureInfo.InvariantCulture);
        IsToday = isToday;
    }

    public DateOnly Date { get; }
    public string DayName { get; }
    public string DayNumber { get; }
    public bool IsToday { get; }

    /// <summary>Zero-based slot in the visible week strip.</summary>
    public int Position { get; set; }
}

public partial class MealSectionUiGroup : ObservableObject
{
    private const int MaxSummaryIngredients = 4;
    private const float PercentScale = 100f;

    [ObservableProperty] private bool _hasItems;

    [ObservableProperty] private string _summaryText = "0 kcal";

    [ObservableProperty] private string _timeText = string.Empty;

    [ObservableProperty] private string _caloriesText = "0 kcal";

    [ObservableProperty] private string _macrosText = "P 0g · C 0g · F 0g";

    [ObservableProperty] private bool _canEditMeal;

    [ObservableProperty] private string _cardName = string.Empty;

    [ObservableProperty] private string _ingredientsText = string.Empty;

    [ObservableProperty] private string _targetShareText = string.Empty;

    [ObservableProperty] private bool _showItemRows;

    public MealSectionUiGroup(MealType mealType, string title)
    {
        MealType = mealType;
        Title = title;
    }

    public MealType MealType { get; }
    public string Title { get; }
    public string MealTypeIconData => MealTypeVisuals.GetIconData(MealType.ToString());

    public ObservableCollection<FoodLogEntry> Items { get; } = [];

    public void RefreshSummary(float dailyTargetCalories = 0f)
    {
        HasItems = Items.Count > 0;
        var cals = Math.Round(Items.Sum(i => i.Calories), 0);
        var p = Math.Round(Items.Sum(i => i.Protein), 1);
        var c = Math.Round(Items.Sum(i => i.Carbs), 1);
        var f = Math.Round(Items.Sum(i => i.Fat), 1);

        SummaryText = Items.Count == 0
            ? "0 kcal"
            : $"{cals:F0} kcal · {p:N0}g P · {c:N0}g C · {f:N0}g F";
        CaloriesText = $"{cals:F0} kcal";
        // Same N0 macro summary as Nutrition's meal card; one block reads the same on both pages.
        MacrosText = $"P {p:N0}g · C {c:N0}g · F {f:N0}g";
        TimeText = Items.Count == 0
            ? string.Empty
            : Items[0].LoggedAtTimestamp.ToString("t", CultureInfo.CurrentCulture);
        CardName = Items.Count switch
        {
            0 => UiTextCatalog.Get("Nutrition_FoodLog_NothingLoggedYet"),
            1 when Items[0].IsGroupedMeal || Items[0].LoggedMealId is > 0 => Items[0].Name,
            1 => string.Empty,
            _ => UiTextCatalog.Format("Nutrition_FoodLog_FoodCountFormat", Items.Count)
        };

        var ingredientNames = Items.Count == 1
            ? Items[0].Ingredients.Select(i => i.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxSummaryIngredients)
                .ToList()
            : Items.Select(i => i.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxSummaryIngredients)
                .ToList();
        var ingredientSummary = string.Join(" + ", ingredientNames);
        IngredientsText = string.Equals(ingredientSummary, CardName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : ingredientSummary;

        var share = dailyTargetCalories > 0 ? cals / dailyTargetCalories * PercentScale : 0;
        TargetShareText = dailyTargetCalories > 0
            ? UiTextCatalog.Format("Nutrition_MealCard_TargetShareFormat", Math.Round(share))
            : string.Empty;
        CanEditMeal = Items.Count == 1 &&
                      (Items[0].IsGroupedMeal || Items[0].LoggedMealId is > 0);
        ShowItemRows = Items.Count > 1 || (Items.Count == 1 && !CanEditMeal);
    }
}
