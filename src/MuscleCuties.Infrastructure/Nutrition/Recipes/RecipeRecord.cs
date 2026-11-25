namespace MuscleCuties.Infrastructure.Nutrition.Recipes;

/// <summary>A saved recipe: its name and the whole recipe stored as a snapshot.</summary>
public sealed class RecipeRecord
{
    /// <summary>The recipe's identity.</summary>
    public Guid Id { get; set; }
    /// <summary>The account that owns it.</summary>
    public int UserId { get; set; }
    /// <summary>The name she gave it.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The recipe as stored JSON, so a recipe can gain fields without a migration.</summary>
    public string SnapshotJson { get; set; } = string.Empty;
    /// <summary>When it was saved, in UTC so the ordering is stable across time zones.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
