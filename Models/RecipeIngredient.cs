namespace MuscleCuties.Models;

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int FoodItemId { get; set; }
    public float Grams { get; set; }

    public Recipe? Recipe { get; set; }
    public FoodItem? FoodItem { get; set; }
}