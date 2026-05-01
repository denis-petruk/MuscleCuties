namespace MuscleCuties.Core.Models.Entities;

public class FoodSyncLog
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ItemsUpserted { get; set; }
    public int ItemsFailed { get; set; }
    public string Status { get; set; } = null!; // Running | Success | Partial | Failed
    public string? ErrorDetails { get; set; }
}