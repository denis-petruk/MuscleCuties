namespace MuscleCuties.Core.Models.Entities;

public class NutritionRecommendation
{
    public int Id { get; set; }
    public int RecommendationSetId { get; set; }
    public int? MealTemplateId { get; set; }
    public string? Notes { get; set; }
    public DateTime? ActedOnAt { get; set; }
    public int? ActedOnLoggedMealId { get; set; }

    public RecommendationSet? RecommendationSet { get; set; }
    public MealTemplate? MealTemplate { get; set; }
    public LoggedMeal? ActedOnLoggedMeal { get; set; }
}