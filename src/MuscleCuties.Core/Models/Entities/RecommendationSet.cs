using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class RecommendationSet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public CyclePhase CyclePhase { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public User? User { get; set; }
    public ICollection<NutritionRecommendation> NutritionRecommendations { get; set; } = new List<NutritionRecommendation>();
    public ICollection<WorkoutRecommendation> WorkoutRecommendations { get; set; } = new List<WorkoutRecommendation>();
    public ICollection<WellnessRecommendation> WellnessRecommendations { get; set; } = new List<WellnessRecommendation>();
}