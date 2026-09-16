using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class LoggedMeal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public DateTime LoggedAt { get; set; }
    public MealType MealType { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<LoggedMealIngredient> Entries { get; set; } = [];
}

public class LoggedMealIngredient
{
    public int Id { get; set; }
    public int LoggedMealId { get; set; }
    public int FoodItemId { get; set; }
    public float Grams { get; set; }

    public LoggedMeal? LoggedMeal { get; set; }
    public FoodItem? FoodItem { get; set; }
}
