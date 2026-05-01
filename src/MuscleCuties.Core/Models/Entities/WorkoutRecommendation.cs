namespace MuscleCuties.Core.Models.Entities;

public class WorkoutRecommendation
{
    public int Id { get; set; }
    public int RecommendationSetId { get; set; }
    public int? WorkoutDayId { get; set; }
    public string? Notes { get; set; }
    public DateTime? ActedOnAt { get; set; }
    public int? ActedOnWorkoutLogId { get; set; }

    public RecommendationSet? RecommendationSet { get; set; }
    public WorkoutDay? WorkoutDay { get; set; }
    public WorkoutLog? ActedOnWorkoutLog { get; set; }
}