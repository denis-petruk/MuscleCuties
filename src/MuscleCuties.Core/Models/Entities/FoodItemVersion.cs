namespace MuscleCuties.Core.Models.Entities;

public class FoodItemVersion
{
    public int Id { get; set; }
    public int FoodItemId { get; set; }
    public string NutrientJson { get; set; } = null!;
    public DateTime VersionedAt { get; set; }
    public string ChangeSource { get; set; } = null!; // FDC | User

    public FoodItem? FoodItem { get; set; }
}