
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One editable recipe ingredient. A catalog-backed row rescales its macros when the grams change,</summary>
/// because the nutrition editor would otherwise allow a portion that disagrees with the catalog.</summary>
public partial class RecipeIngredientEditor : ObservableObject
{
    private const float EmptyNutrientValue = 0f;
    private const float DefaultCatalogSnapshotGrams = 100f;
    /// <summary>The "0" placeholder for an empty nutrient field, written in the current culture so the parse</summary>
    /// round-trips.</summary>
    private static string EmptyNutrientText => EmptyNutrientValue.ToString(CultureInfo.CurrentCulture);

    private float? _baseSnapshotGrams;
    private float? _baseSnapshotCalories;
    private float? _baseSnapshotProtein;
    private float? _baseSnapshotCarbs;
    private float? _baseSnapshotFats;
    private bool _isScalingFromSnapshot;

    [ObservableProperty] private Guid? _foodItemId;
    [ObservableProperty] private string? _sourceKind = "Manual";
    [ObservableProperty] private string? _sourceLabel;
    [ObservableProperty] private string? _sourceVersion;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _grams = string.Empty;
    [ObservableProperty] private string _calories = EmptyNutrientText;
    [ObservableProperty] private string _protein = EmptyNutrientText;
    [ObservableProperty] private string _carbs = EmptyNutrientText;
    [ObservableProperty] private string _fats = EmptyNutrientText;

    /// <summary>True when the ingredient refers to a catalog food.</summary>
    public bool IsCatalogBacked => FoodItemId.HasValue && FoodItemId.Value != Guid.Empty;

    /// <summary>Applies the catalog values to the ingredient.</summary>
    public void ApplyCatalogSnapshot(
        Guid foodItemId,
        string name,
        float grams,
        float calories,
        float protein,
        float carbs,
        float fats,
        string? sourceKind = "Catalog",
        string? sourceLabel = null,
        string? sourceVersion = null)
    {
        if (foodItemId == Guid.Empty)
            throw new ArgumentException("A catalog food ID is required.", nameof(foodItemId));

        var effectiveGrams = float.IsFinite(grams) && grams > EmptyNutrientValue ? grams : DefaultCatalogSnapshotGrams;
        _baseSnapshotGrams = effectiveGrams;
        _baseSnapshotCalories = MathF.Max(EmptyNutrientValue, calories);
        _baseSnapshotProtein = MathF.Max(EmptyNutrientValue, protein);
        _baseSnapshotCarbs = MathF.Max(EmptyNutrientValue, carbs);
        _baseSnapshotFats = MathF.Max(EmptyNutrientValue, fats);

        _isScalingFromSnapshot = true;
        try
        {
            FoodItemId = foodItemId;
            SourceKind = sourceKind ?? "Catalog";
            SourceLabel = sourceLabel;
            SourceVersion = sourceVersion;
            Name = name.Trim();
            Grams = effectiveGrams.ToString(CultureInfo.CurrentCulture);
            Calories = _baseSnapshotCalories.Value.ToString(CultureInfo.CurrentCulture);
            Protein = _baseSnapshotProtein.Value.ToString(CultureInfo.CurrentCulture);
            Carbs = _baseSnapshotCarbs.Value.ToString(CultureInfo.CurrentCulture);
            Fats = _baseSnapshotFats.Value.ToString(CultureInfo.CurrentCulture);
        }
        finally
        {
            _isScalingFromSnapshot = false;
        }
    }

    /// <summary>Recalculates the macros when the grams change.</summary>
    partial void OnGramsChanged(string value)
    {
        if (_isScalingFromSnapshot || !IsCatalogBacked || !_baseSnapshotGrams.HasValue || _baseSnapshotGrams.Value <= EmptyNutrientValue)
            return;

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var newGrams) ||
            !float.IsFinite(newGrams) || newGrams <= EmptyNutrientValue)
            return;

        var scale = newGrams / _baseSnapshotGrams.Value;
        _isScalingFromSnapshot = true;
        try
        {
            Calories = ((_baseSnapshotCalories ?? EmptyNutrientValue) * scale).ToString(CultureInfo.CurrentCulture);
            Protein = ((_baseSnapshotProtein ?? EmptyNutrientValue) * scale).ToString(CultureInfo.CurrentCulture);
            Carbs = ((_baseSnapshotCarbs ?? EmptyNutrientValue) * scale).ToString(CultureInfo.CurrentCulture);
            Fats = ((_baseSnapshotFats ?? EmptyNutrientValue) * scale).ToString(CultureInfo.CurrentCulture);
        }
        finally
        {
            _isScalingFromSnapshot = false;
        }
    }
}
