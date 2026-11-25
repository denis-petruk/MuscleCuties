using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Entities.Users;

/// <summary>The profile row: the answers onboarding collected, the unit choices they are displayed in, and the stored JSON</summary>
/// for the values that are structures rather than columns.</summary>
public class UserProfile
{
    /// <summary>The stored identity.</summary>
    public int Id { get; set; }
    /// <summary>The account this profile belongs to; one profile per account.</summary>
    public int UserId { get; set; }
    [Required] public string Name { get; set; } = null!;
    /// <summary>The birth date, which the app never displays directly.</summary>
    public DateTime DateOfBirth { get; set; }
    /// <summary>Height stored in centimetres whatever the display unit is.</summary>
    public float Height { get; set; }
    /// <summary>Weight stored in kilograms, converted only for display.</summary>
    public float Weight { get; set; }
    /// <summary>The training goal the plan is built for.</summary>
    public UserGoal Goal { get; set; }
    /// <summary>How fast the goal should be pursued, which only applies to the goals that use a weight target.</summary>
    public WeightGoalPace WeightGoalPace { get; set; }
    /// <summary>The experience level that decides which plan styles are offered.</summary>
    public TrainingExperienceLevel TrainingExperienceLevel { get; set; }
    /// <summary>The phase last recorded; null before any cycle has been logged.</summary>
    public CyclePhase? CurrentCyclePhase { get; set; }
    /// <summary>How many days a week the plan should train.</summary>
    public int WorkoutDaysPerWeek { get; set; }
    /// <summary>The cycle length the prediction uses, which is her own rather than a default once known.</summary>
    public int CycleLength { get; set; }
    /// <summary>The dietary tags as a comma-separated list, which is the stored form the plan filters on.</summary>
    public string DietaryTags { get; set; } = string.Empty;
    /// <summary>The session length the plan targets, defaulting to an hour before she answers.</summary>
    public int SessionDurationMinutes { get; set; } = 60;
    /// <summary>The equipment the plan may assume, defaulting to a full gym until she says otherwise.</summary>
    public string EquipmentLevel { get; set; } = "FullGym";
    /// <summary>Her own phase baselines as JSON, empty until she has recorded any.</summary>
    public string PhaseBaselinesJson { get; set; } = string.Empty;
    /// <summary>The activities she wants in her plans, stored as the list the planner reads.</summary>
    public string PreferredWorkoutActivityTypes { get; set; } = string.Empty;
    /// <summary>The unit system chosen for display, metric until she changes it.</summary>
    public string UnitSystem { get; set; } = "Metric";
    /// <summary>The unit body weight is displayed in.</summary>
    public string BodyWeightUnit { get; set; } = "kg";
    /// <summary>The unit food mass is displayed in.</summary>
    public string FoodMassUnit { get; set; } = "g";
    /// <summary>The unit height is displayed in.</summary>
    public string HeightUnit { get; set; } = "cm";
    /// <summary>The unit distance is displayed in.</summary>
    public string DistanceUnit { get; set; } = "km";
    /// <summary>The unit energy is displayed in.</summary>
    public string EnergyUnit { get; set; } = "kcal";
    /// <summary>The nutrition targets as JSON, empty while they are still the calculated ones.</summary>
    public string NutritionGoalsJson { get; set; } = string.Empty;
    /// <summary>The reproductive-context answers as JSON; health data, so it stays on the device and out of logs.</summary>
    public string ReproductiveContextJson { get; set; } = string.Empty;
    /// <summary>The avatar's local path, empty when she has not chosen one.</summary>
    public string ProfileImagePath { get; set; } = string.Empty;
    /// <summary>When the profile last changed.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>The navigation to the owning account.</summary>
    public User? User { get; set; }
}
