using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

/// <summary>A catalog food: its macros and micronutrients per 100 g, the branded metadata when it has any, and its stored</summary>
/// serving choices.</summary>
public class FoodItem
{
    /// <summary>Identity of the stored food item.</summary>
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    /// <summary>Energy per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Calories { get; set; }
    /// <summary>Protein per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Protein { get; set; }
    /// <summary>Carbohydrate per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Carbs { get; set; }
    /// <summary>Fat per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Fats { get; set; }
    /// <summary>Fibre per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Fiber { get; set; }
    /// <summary>Iron per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Iron { get; set; }
    /// <summary>Vitamin B12 per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float VitaminB12 { get; set; }
    /// <summary>Vitamin C per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float VitaminC { get; set; }
    /// <summary>Vitamin D per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float VitaminD { get; set; }
    /// <summary>Vitamin A per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float VitaminA { get; set; }
    /// <summary>Vitamin B6 per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float VitaminB6 { get; set; }
    /// <summary>Folate per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Folate { get; set; }
    /// <summary>Calcium per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Calcium { get; set; }
    /// <summary>Magnesium per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Magnesium { get; set; }
    /// <summary>Zinc per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Zinc { get; set; }
    /// <summary>Potassium per 100 g, as stored for 100 g, which is what every scaling in the app assumes.</summary>
    public float Potassium { get; set; }
    /// <summary>True for a food the user created, which is why it has an owner and no external id.</summary>
    public bool IsCustom { get; set; }
    /// <summary>The owner of a custom food; null for a shared catalog food, which is what keeps one user's food out of</summary>
    /// another's search.</summary>
    public int? UserId { get; set; }
    /// <summary>The navigation to that owner.</summary>
    public User? User { get; set; }
    /// <summary>The external database's id, kept so a resync updates this row instead of adding a duplicate.</summary>
    public int? FdcId { get; set; }
    /// <summary>The external source's own type label for the food.</summary>
    public string? DataType { get; set; }
    /// <summary>The brand's owner, as the source reports it.</summary>
    public string? BrandOwner { get; set; }
    /// <summary>The brand the food is sold under.</summary>
    public string? BrandName { get; set; }
    /// <summary>The barcode, which is how a scan finds a branded food.</summary>
    public string? GtinUpc { get; set; }
    /// <summary>The label's ingredient text, as the source gives it.</summary>
    public string? Ingredients { get; set; }
    /// <summary>The amount of one branded serving, which is the unit the food is logged in rather than grams.</summary>
    public float? ServingSize { get; set; }
    /// <summary>The unit that amount is measured in.</summary>
    public string? ServingSizeUnit { get; set; }
    /// <summary>The branded serving choices as stored JSON; empty for a food without them, where grams are the only</summary>
    /// option.</summary>
    public string? ServingOptionsJson { get; set; }
    /// <summary>When the external data was last refreshed, or null while it has never been.</summary>
    public DateTime? LastSyncedAt { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the row last changed, which is what a resync compares against.</summary>
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    /// <summary>A food is treated as branded when it carries a source type and a barcode, because that is when the serving</summary>
    /// editor has something to show.</summary>
    public bool IsBranded =>
        string.Equals(DataType, "Branded", StringComparison.OrdinalIgnoreCase);

    /// <summary>The diary entries that reference this food.</summary>
    public ICollection<LoggedMealEntry> LoggedMealEntries { get; set; } = new List<LoggedMealEntry>();
    /// <summary>The stored history of this food's changed values.</summary>
    public ICollection<FoodItemVersion> Versions { get; set; } = new List<FoodItemVersion>();
}
