namespace MuscleCuties.Infrastructure.Nutrition.Recipes;

public sealed class RecipeRecord
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
