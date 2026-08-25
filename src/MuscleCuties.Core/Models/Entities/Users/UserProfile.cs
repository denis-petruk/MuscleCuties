using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Entities.Users;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public UserGoal Goal { get; set; }
    public WeightGoalPace WeightGoalPace { get; set; }
    public TrainingExperienceLevel TrainingExperienceLevel { get; set; }
    public CycleTrackingMode CycleTrackingMode { get; set; }
    public CyclePhase? CurrentCyclePhase { get; set; }
    public int WorkoutDaysPerWeek { get; set; }
    public int CycleLength { get; set; }
    public string DietaryTags { get; set; } = string.Empty;
    public string PreferredWorkoutActivityTypes { get; set; } = string.Empty;
    public string UnitSystem { get; set; } = "Metric";
    public string BodyWeightUnit { get; set; } = "kg";
    public string FoodMassUnit { get; set; } = "g";
    public string HeightUnit { get; set; } = "cm";
    public string DistanceUnit { get; set; } = "km";
    public string EnergyUnit { get; set; } = "kcal";
    public string NutritionGoalsJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
