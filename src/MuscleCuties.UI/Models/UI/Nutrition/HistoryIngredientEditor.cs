using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Nutrition;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public partial class HistoryIngredientEditor(HistoryIngredientDto ingredient) : ObservableObject
{
    [ObservableProperty] private float _grams = ingredient.Grams;
    public int FoodItemId { get; } = ingredient.FoodItemId;
    public string Name { get; } = ingredient.Name;
}
