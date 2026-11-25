
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>A quick-add row with its own selection state, so the list can show a checkmark without</summary>
/// rebuilding the items.</summary>
public partial class QuickFoodItemUiModel : ObservableObject
{
    private readonly Action<QuickFoodItemUiModel>? _onSelectionChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionAccessibilityText), nameof(SelectionStateText))]
    private bool _isSelected;

    /// <summary>Wraps a quick add and optionally reports selection changes back to the owning view model.</summary>
    public QuickFoodItemUiModel(QuickFood item, Action<QuickFoodItemUiModel>? onSelectionChanged = null)
    {
        Item = item;
        _onSelectionChanged = onSelectionChanged;
    }

    /// <summary>The quick food this row presents.</summary>
    public QuickFood Item { get; }

    /// <summary>S01: the visual checkmark alone does not communicate selection to VoiceOver.</summary>
    public string SelectionAccessibilityText =>
        $"{Name} — {SelectionStateText}";

    /// <summary>Selection is a state trait of the row, not part of its name.</summary>
    public string SelectionStateText =>
        UiTextCatalog.Get(IsSelected
            ? "Nutrition_QuickFood_Selected"
            : "Nutrition_QuickFood_NotSelected");

    /// <summary>The food's name.</summary>
    public string Name => Item.Name;
    /// <summary>The serving size in the food's unit.</summary>
    public double ServingSize => Item.ServingSize;
    /// <summary>The unit the serving is measured in.</summary>
    public string ServingUnit => Item.ServingUnit;
    /// <summary>How many servings the entry counts.</summary>
    public double ServingsCount => Item.ServingsCount;
    /// <summary>The calories in the serving.</summary>
    public double Calories => Item.Calories;
    /// <summary>The protein in the serving.</summary>
    public double Protein => Item.Protein;
    /// <summary>The carbs in the serving.</summary>
    public double Carbs => Item.Carbs;
    /// <summary>The fat in the serving.</summary>
    public double Fat => Item.Fat;
    /// <summary>The supporting detail line.</summary>
    public string Detail => Item.Detail;
    /// <summary>The catalog id of the source food.</summary>
    public Guid? SourceId => Item.SourceId;
    /// <summary>The ingredient food id when this is part of a recipe.</summary>
    public int? IngredientFoodItemId => Item.IngredientFoodItemId;
    /// <summary>The ingredients when the food is a composed recipe.</summary>
    public IReadOnlyList<FoodHistoryIngredient> Ingredients => Item.Ingredients;

    /// <summary>Refreshes the selection visuals when the row is toggled.</summary>
    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke(this);
    }
}
