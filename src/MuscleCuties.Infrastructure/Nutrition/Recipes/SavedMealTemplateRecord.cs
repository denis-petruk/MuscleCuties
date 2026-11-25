namespace MuscleCuties.Infrastructure.Nutrition.Recipes;

/// <summary>A saved meal template: a meal she can log again with one tap.</summary>
public sealed class SavedMealTemplateRecord
{
    /// <summary>The template's identity.</summary>
    public Guid Id { get; set; }
    /// <summary>The account that owns it.</summary>
    public int UserId { get; set; }
    /// <summary>The name she gave it.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Which meal it is meant to be logged as.</summary>
    public int MealType { get; set; }
    /// <summary>The template's contents as stored JSON.</summary>
    public string SnapshotJson { get; set; } = string.Empty;
    /// <summary>When it was saved, in UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
