using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class FoodItemEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DailyLogId { get; set; }

    public string Date { get; set; } =
        string.Empty; // ISO-8601 string YYYY-MM-DD for fast composite index (Date, MealType)

    public MealType MealType { get; set; }
    public string Name { get; set; } = string.Empty;
    public double ServingSize { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public double ServingsCount { get; set; } = 1.0;
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public DateTime LoggedAtTimestamp { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DailyNutritionLog? DailyLog { get; set; }
}
