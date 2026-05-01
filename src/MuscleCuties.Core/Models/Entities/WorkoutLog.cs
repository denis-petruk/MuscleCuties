namespace MuscleCuties.Core.Models.Entities;

public class WorkoutLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorkoutDayId { get; set; }
    public DateTime Date { get; set; }
    public int CompletionPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public WorkoutDay? WorkoutDay { get; set; }
}