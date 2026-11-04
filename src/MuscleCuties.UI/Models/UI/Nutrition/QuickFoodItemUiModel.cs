using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public partial class QuickFoodItemUiModel : ObservableObject
{
    private readonly Action<QuickFoodItemUiModel>? _onSelectionChanged;

    [ObservableProperty]
    private bool _isSelected;

    public QuickFoodItemUiModel(QuickFood item, Action<QuickFoodItemUiModel>? onSelectionChanged = null)
    {
        Item = item;
        _onSelectionChanged = onSelectionChanged;
    }

    public QuickFood Item { get; }

    public string Name => Item.Name;
    public double ServingSize => Item.ServingSize;
    public string ServingUnit => Item.ServingUnit;
    public double ServingsCount => Item.ServingsCount;
    public double Calories => Item.Calories;
    public double Protein => Item.Protein;
    public double Carbs => Item.Carbs;
    public double Fat => Item.Fat;
    public string Detail => Item.Detail;
    public Guid? SourceId => Item.SourceId;
    public int? IngredientFoodItemId => Item.IngredientFoodItemId;
    public IReadOnlyList<FoodHistoryIngredient> Ingredients => Item.Ingredients;

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke(this);
    }
}
