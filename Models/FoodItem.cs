using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Models;

public class FoodItem
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }
    public bool IsCustom { get; set; }

    public ICollection<FoodLog> FoodLogs { get; set; } = new List<FoodLog>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}