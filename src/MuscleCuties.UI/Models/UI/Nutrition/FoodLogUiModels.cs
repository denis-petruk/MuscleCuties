using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;

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
}

public partial class MealSectionUiGroup : ObservableObject
{
    [ObservableProperty] private bool _hasItems;

    [ObservableProperty] private bool _isExpanded = true;

    [ObservableProperty] private string _summaryText = "0 kcal";

    public MealSectionUiGroup(MealType mealType, string title)
    {
        MealType = mealType;
        Title = title;
    }

    public MealType MealType { get; }
    public string Title { get; }

    public ObservableCollection<FoodItemEntryDto> Items { get; } = [];

    public void RefreshSummary()
    {
        HasItems = Items.Count > 0;
        var cals = Math.Round(Items.Sum(i => i.Calories), 0);
        var p = Math.Round(Items.Sum(i => i.Protein), 1);
        var c = Math.Round(Items.Sum(i => i.Carbs), 1);
        var f = Math.Round(Items.Sum(i => i.Fat), 1);

        SummaryText = Items.Count == 0
            ? "0 kcal"
            : $"{cals:F0} kcal · {p:F1}g P · {c:F1}g C · {f:F1}g F";
    }
}
