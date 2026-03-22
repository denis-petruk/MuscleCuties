using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Models;

public class Recipe
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    [Required] public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public float TotalCalories { get; set; }
    public float TotalProtein { get; set; }
    public float TotalCarbs { get; set; }
    public float TotalFats { get; set; }
    public string? DietaryTags { get; set; }
    public CyclePhase? RecommendedPhase { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}