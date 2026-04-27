using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public bool UseMetricSystem { get; set; } = true;
    public UserGoal Goal { get; set; }
    public int WorkoutDaysPerWeek { get; set; }
    public int CycleLength { get; set; }
    public WeightGoalPace WeightGoalPace { get; set; }
    public DietaryTag DietaryPreference { get; set; }
    public int ExperienceLevel { get; set; }

    public User? User { get; set; }
}
