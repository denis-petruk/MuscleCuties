using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public enum MealFoodSource
{
    Search,
    Barcode,
    Custom
}

public sealed partial class MealFoodSourceTab : ObservableObject
{
    public MealFoodSourceTab(MealFoodSource source) => Source = source;

    public MealFoodSource Source { get; }

    [ObservableProperty] private bool _isSelected;
}