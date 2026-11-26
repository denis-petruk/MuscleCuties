using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One day in the diary's week strip, carrying its own selected state.</summary>
public partial class DateStripDayItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    /// <summary>Builds the cell's labels once, so scrolling the strip does not reformat them.</summary>
    public DateStripDayItem(DateOnly date, bool isToday)
    {
        Date = date;
        DayName = DisplayFormat.DayOfWeekShortUpper(date.ToDateTime(TimeOnly.MinValue));
        DayNumber = DisplayFormat.DayNumber(date);
        IsToday = isToday;
    }

    /// <summary>The day this strip cell shows.</summary>
    public DateOnly Date { get; }
    /// <summary>The cell's weekday name.</summary>
    public string DayName { get; }
    /// <summary>The cell's day of the month.</summary>
    public string DayNumber { get; }
    /// <summary>True when the cell is today.</summary>
    public bool IsToday { get; }

    /// <summary>True when the cell shows the today marker.</summary>
    public bool ShowTodayDot => IsToday && !IsSelected;

    /// <summary>Spoken label for the cell: the date, plus "today" when it is.</summary>
    public string AccessibilityDescription => IsToday
        ? $"{DisplayFormat.DayMonthWithWeekday(Date)}, {UiTextCatalog.Get("Common_Today")}"
        : DisplayFormat.DayMonthWithWeekday(Date);

    /// <summary>Zero-based slot in the visible week strip.</summary>
    public int Position { get; set; }

    /// <summary>Refreshes the marker when the cell's selection changes.</summary>
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTodayDot));
    }
}

/// <summary>One meal's card in the diary: its totals, the single line that names what was eaten, and</summary>
/// whether the meal can be edited as one unit or must be opened row by row.</summary>
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

    /// <summary>Creates the section for a meal type; its items are added afterwards.</summary>
    public MealSectionUiGroup(MealType mealType, string title)
    {
        MealType = mealType;
        Title = title;
    }

    /// <summary>The meal this section groups.</summary>
    public MealType MealType { get; }
    /// <summary>The section's heading.</summary>
    public string Title { get; }
    /// <summary>Outline glyph for the section's meal type.</summary>
    public string MealTypeIconData => MealTypeVisuals.GetIconData(MealType.ToString());

    /// <summary>The entries in this section.</summary>
    public ObservableCollection<FoodLogEntry> Items { get; } = [];

    /// <summary>Recalculates the card from its items: calorie and macro totals, the name shown, the ingredient
    /// line, the share of the daily target, and whether rows or the edit action are shown.</summary>
    public void RefreshSummary(float dailyTargetCalories = 0f)
    {
        HasItems = Items.Count > 0;
        var cals = Math.Round(Items.Sum(i => i.Calories), 0);
        var p = Math.Round(Items.Sum(i => i.Protein), 1);
        var c = Math.Round(Items.Sum(i => i.Carbs), 1);
        var f = Math.Round(Items.Sum(i => i.Fat), 1);

        SummaryText = Items.Count == 0
            ? DisplayFormat.Calories(0)
            : $"{DisplayFormat.Calories(cals)} · {DisplayFormat.Macros(p, c, f)}";
        CaloriesText = DisplayFormat.Calories(cals);
        // Same N0 macro summary as Nutrition's meal card; one block reads the same on both pages.
        MacrosText = DisplayFormat.Macros(p, c, f);
        TimeText = Items.Count == 0
            ? string.Empty
            : DisplayFormat.Time(Items[0].LoggedAtTimestamp);
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
