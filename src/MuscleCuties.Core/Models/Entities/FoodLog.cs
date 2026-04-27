using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class FoodLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FoodItemId { get; set; }
    public DateTime Date { get; set; }
    public float Grams { get; set; }
    public MealType MealType { get; set; }

    public FoodItem? FoodItem { get; set; }
}
