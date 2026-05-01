namespace MuscleCuties.Core.Models.Entities;

public class LoggedMealEntry
{
    public int Id { get; set; }
    public int LoggedMealId { get; set; }
    public int FoodItemId { get; set; }
    public float Grams { get; set; }

    public LoggedMeal? LoggedMeal { get; set; }
    public FoodItem? FoodItem { get; set; }
}