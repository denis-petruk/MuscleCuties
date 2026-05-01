namespace MuscleCuties.Core.Models.Entities;

public class MealTemplateEntry
{
    public int Id { get; set; }
    public int MealTemplateId { get; set; }
    public int FoodItemId { get; set; }
    public float Grams { get; set; }

    public MealTemplate? MealTemplate { get; set; }
    public FoodItem? FoodItem { get; set; }
}