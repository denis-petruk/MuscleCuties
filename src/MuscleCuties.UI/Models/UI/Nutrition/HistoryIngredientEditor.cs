using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public partial class HistoryIngredientEditor(FoodHistoryIngredient ingredient) : ObservableObject
{
    [ObservableProperty] private float _grams = ingredient.Grams;
    public int FoodItemId { get; } = ingredient.FoodItemId;
    public string Name { get; } = ingredient.Name;
}
