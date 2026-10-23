namespace MuscleCuties.Core.Models.Entities.Workout.Planning;

public class WorkoutPlanningConfigEntry
{
    public int Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
