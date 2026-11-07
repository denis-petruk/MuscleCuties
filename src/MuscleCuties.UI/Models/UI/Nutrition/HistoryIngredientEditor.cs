using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;

namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>
/// One editable ingredient row. NM02: the entry edits a locale-aware draft string, and the canonical
/// grams value is only replaced by a deliberate, valid edit. An untouched row therefore round-trips
/// its original quantity instead of being re-serialized from a rounded display value.
/// </summary>
public partial class HistoryIngredientEditor : ObservableObject
{
    private readonly float _originalGrams;
    private readonly string _originalText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Grams))]
    [NotifyPropertyChangedFor(nameof(HasAmountError))]
    [NotifyPropertyChangedFor(nameof(AmountErrorText))]
    [NotifyPropertyChangedFor(nameof(HasMacros))]
    [NotifyPropertyChangedFor(nameof(MacrosForDraft))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private string _gramsText;

    private readonly float _caloriesPer100Grams;
    private readonly float _proteinPer100Grams;
    private readonly float _carbsPer100Grams;
    private readonly float _fatsPer100Grams;

    public HistoryIngredientEditor(FoodHistoryIngredient ingredient)
        : this(ingredient, 0f, 0f, 0f, 0f)
    {
    }

    /// <summary>
    /// Carries the ingredient's per-100 g macros so a row can show what the portion it is about to
    /// save actually contains, instead of only its name and a bare number field.
    /// </summary>
    public HistoryIngredientEditor(
        FoodHistoryIngredient ingredient,
        float caloriesPer100Grams,
        float proteinPer100Grams,
        float carbsPer100Grams,
        float fatsPer100Grams)
    {
        FoodItemId = ingredient.FoodItemId;
        Name = ingredient.Name;
        _originalGrams = ingredient.Grams;
        _originalText = IngredientPortionDraft.Format(ingredient.Grams, CultureInfo.CurrentCulture);
        _gramsText = _originalText;
        _caloriesPer100Grams = caloriesPer100Grams;
        _proteinPer100Grams = proteinPer100Grams;
        _carbsPer100Grams = carbsPer100Grams;
        _fatsPer100Grams = fatsPer100Grams;
    }

    public int FoodItemId { get; }

    public string Name { get; }

    public bool HasMacros => _caloriesPer100Grams > 0f || _proteinPer100Grams > 0f ||
                             _carbsPer100Grams > 0f || _fatsPer100Grams > 0f;

    /// <summary>Macros for the quantity currently drafted, so the row totals follow the edit.</summary>
    public NutritionMacros MacrosForDraft => NutritionPresentationMapper.FromPer100Grams(
        _caloriesPer100Grams, _proteinPer100Grams, _carbsPer100Grams, _fatsPer100Grams, Grams);

    public string DetailText => $"{Grams:N0} g · {MacrosForDraft.NutritionText}";

    /// <summary>
    /// The quantity to persist. An untouched or invalid draft keeps the original value, so a failed
    /// edit can never round or corrupt stored portions.
    /// </summary>
    public float Grams => TryGetGrams(out var grams) ? grams : _originalGrams;

    public bool HasAmountError => !TryGetGrams(out _);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1822:Mark members as static",
        Justification = "Target of compiled XAML bindings; must stay an instance member.")]
    public string AmountErrorText => UiTextCatalog.Get("Nutrition_FoodLog_AmountError");

    /// <summary>True while the draft still equals the value this row was opened with.</summary>
    public bool IsUnchanged => string.Equals(GramsText, _originalText, StringComparison.Ordinal);

    public bool TryGetGrams(out float grams) =>
        IngredientPortionDraft.TryParse(GramsText, _originalText, _originalGrams,
            CultureInfo.CurrentCulture, out grams);
}
