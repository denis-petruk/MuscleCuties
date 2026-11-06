using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;
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
    private string _gramsText;

    public HistoryIngredientEditor(FoodHistoryIngredient ingredient)
    {
        FoodItemId = ingredient.FoodItemId;
        Name = ingredient.Name;
        _originalGrams = ingredient.Grams;
        _originalText = IngredientPortionDraft.Format(ingredient.Grams, CultureInfo.CurrentCulture);
        _gramsText = _originalText;
    }

    public int FoodItemId { get; }

    public string Name { get; }

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
