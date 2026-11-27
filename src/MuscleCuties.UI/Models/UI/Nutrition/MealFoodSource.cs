
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>Where the food being added comes from: the search, a barcode scan, or a manual entry.</summary>
public enum MealFoodSource
{
    Search,
    Barcode,
    Custom
}

/// <summary>One tab in the meal editor's source switcher.</summary>
public sealed partial class MealFoodSourceTab : ObservableObject
{
    /// <summary>Wraps a food source as a selectable tab.</summary>
    public MealFoodSourceTab(MealFoodSource source) => Source = source;

    /// <summary>The source this tab opens.</summary>
    public MealFoodSource Source { get; }

    [ObservableProperty] private bool _isSelected;
}
