using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Models;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public int Age { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public UserGoal Goal { get; set; }
    public int WorkoutDaysPerWeek { get; set; }
    public int CycleLength { get; set; }
    public bool UseMetricSystem { get; set; } = true;

    public User? User { get; set; }
}