
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One selectable portion in the picker.</summary>
public class FoodServingOptionItem
{
    /// <summary>Portion name shown on the row.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Unit the portion is measured in.</summary>
    public string Unit { get; set; } = string.Empty;
    /// <summary>Weight the portion represents, which is what gets logged.</summary>
    public float Grams { get; set; }
    /// <summary>Where the option came from (the food itself or the standard set), so duplicates from different</summary>
    /// origins stay distinguishable.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Localized label, translated at display time because the label may come from stored data.</summary>
    public string DisplayText => UiTextCatalog.TranslateRuntimeText(Label);

    /// <summary>The option's label, as shown in the picker.</summary>
    public override string ToString()
    {
        return DisplayText;
    }
}
