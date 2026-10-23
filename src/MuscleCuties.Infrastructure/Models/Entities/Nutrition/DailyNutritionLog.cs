using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class DailyNutritionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string Date { get; set; } = string.Empty; // ISO-8601 string YYYY-MM-DD
    public int WaterIntakeMl { get; set; }
    public double TargetCalories { get; set; }
    public double TargetProtein { get; set; }
    public double TargetCarbs { get; set; }
    public double TargetFat { get; set; }
    public double TotalCalories { get; set; }
    public double TotalProtein { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFat { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<FoodItemEntry> Entries { get; set; } = new Collection<FoodItemEntry>();
}
