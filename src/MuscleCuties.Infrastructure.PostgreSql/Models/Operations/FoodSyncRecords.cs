namespace MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

public sealed class FoodItemVersionRecord
{
    public int Id { get; set; }
    public int FoodItemId { get; set; }
    public string NutrientJson { get; set; } = string.Empty;
    public DateTime VersionedAtUtc { get; set; }
    public string ChangeSource { get; set; } = string.Empty;
}

public sealed class FoodSyncLogRecord
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ItemsUpserted { get; set; }
    public int ItemsFailed { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
}
