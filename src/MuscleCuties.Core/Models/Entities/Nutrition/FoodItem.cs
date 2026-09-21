using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class FoodItem
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }
    public float Fiber { get; set; }
    public float Iron { get; set; }
    public float VitaminB12 { get; set; }
    public float VitaminC { get; set; }
    public float VitaminD { get; set; }
    public float VitaminA { get; set; }
    public float VitaminB6 { get; set; }
    public float Folate { get; set; }
    public float Calcium { get; set; }
    public float Magnesium { get; set; }
    public float Zinc { get; set; }
    public float Potassium { get; set; }
    public bool IsCustom { get; set; }
    public int? FdcId { get; set; }
    public string? DataType { get; set; }
    public string? BrandOwner { get; set; }
    public string? BrandName { get; set; }
    public string? GtinUpc { get; set; }
    public string? Ingredients { get; set; }
    public float? ServingSize { get; set; }
    public string? ServingSizeUnit { get; set; }
    public string? ServingOptionsJson { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public bool IsBranded =>
        string.Equals(DataType, "Branded", StringComparison.OrdinalIgnoreCase);

    public ICollection<LoggedMealEntry> LoggedMealEntries { get; set; } = new List<LoggedMealEntry>();
    public ICollection<FoodItemVersion> Versions { get; set; } = new List<FoodItemVersion>();
}
