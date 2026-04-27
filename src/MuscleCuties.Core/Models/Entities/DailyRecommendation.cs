using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class DailyRecommendation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public CyclePhase Phase { get; set; }
    public int WorkoutPlanId { get; set; }
    public string? WorkoutIntensityNote { get; set; }
    public float TargetCalories { get; set; }
    public float TargetProtein { get; set; }
    public float TargetCarbs { get; set; }
    public float TargetFats { get; set; }
    public string? GeneralTip { get; set; }

    public User? User { get; set; }
    public WorkoutPlan? WorkoutPlan { get; set; }
}
