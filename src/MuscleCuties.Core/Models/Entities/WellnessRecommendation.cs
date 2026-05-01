namespace MuscleCuties.Core.Models.Entities;

public class WellnessRecommendation
{
    public int Id { get; set; }
    public int RecommendationSetId { get; set; }
    public string Category { get; set; } = null!; // Rest | Hydration | Supplement | Mindfulness | IronRich
    public string Notes { get; set; } = null!;
    public DateTime? ActedOnAt { get; set; }

    public RecommendationSet? RecommendationSet { get; set; }
}