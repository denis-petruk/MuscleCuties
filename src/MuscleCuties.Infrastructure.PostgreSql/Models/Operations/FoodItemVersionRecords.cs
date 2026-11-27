namespace MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

/// <summary>A nutrient snapshot taken before a catalog food changed, so a sync's effect can be diffed.</summary>
public sealed class FoodItemVersionRecord
{
    /// <summary>Primary key of the snapshot.</summary>
    public int Id { get; set; }
    /// <summary>The catalog food this snapshot belongs to.</summary>
    public int FoodItemId { get; set; }
    /// <summary>The nutrient values as they were, in the same shape the live row stores.</summary>
    public string NutrientJson { get; set; } = string.Empty;
    /// <summary>When the snapshot was taken.</summary>
    public DateTime VersionedAtUtc { get; set; }
    /// <summary>What caused the change, so a sync can be told apart from a manual edit.</summary>
    public string ChangeSource { get; set; } = string.Empty;
}
