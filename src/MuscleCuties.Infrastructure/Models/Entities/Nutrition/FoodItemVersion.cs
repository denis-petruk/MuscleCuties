namespace MuscleCuties.Core.Models.Entities.Nutrition;

/// <summary>One past snapshot of a food's nutrient values, kept so a change can be traced back.</summary>
public class FoodItemVersion
{
    /// <summary>Identity of the stored food item version.</summary>
    public int Id { get; set; }
    /// <summary>The food this snapshot belongs to.</summary>
    public int FoodItemId { get; set; }
    /// <summary>The nutrient values as they were, in the same stored shape the live row uses.</summary>
    public string NutrientJson { get; set; } = null!;
    /// <summary>When the snapshot was taken.</summary>
    public DateTime VersionedAt { get; set; }
    /// <summary>What caused the change, so a resync can be told apart from a user's edit.</summary>
    public string ChangeSource { get; set; } = null!;

    /// <summary>The navigation to the food.</summary>
    public FoodItem? FoodItem { get; set; }
}
