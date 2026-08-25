using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.UI.Nutrition;

public class MealTemplateItem
{
    public int MealTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MealType MealType { get; set; }
    public string MealTypeText => MealType.ToString().ToUpperInvariant();
    public string SummaryText { get; set; } = string.Empty;
    public IReadOnlyList<MealIngredientItem> Ingredients { get; set; } = [];
}
