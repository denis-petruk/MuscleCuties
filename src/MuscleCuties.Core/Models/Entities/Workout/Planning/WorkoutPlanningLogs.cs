namespace MuscleCuties.Core.Models.Entities.Workout.Planning;

public class DailyReadinessLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public double SleepHours { get; set; }
    public double Sleep3dAvg { get; set; }
    public int StepsYesterday { get; set; }
    public int Steps7dAvg { get; set; }
    public int Energy { get; set; }
    public int Pain { get; set; }
    public bool Bloating { get; set; }
    public double? WeightKg { get; set; }
    public int ReadinessScore { get; set; }
    public int ReadinessTier { get; set; }
    public string Phase { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
