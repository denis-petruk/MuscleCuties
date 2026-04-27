using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Models;

public class QuizAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    [Required] public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }
    
    // The value written to UserProfile/UserBaselineProfile when this answer is selected.
    // For enums (Goal, DietaryPreference) this is the int cast of the enum value.
    // For phase symptoms (Pain, Energy, Mood) this is a 1–5 scale.
    // For WorkoutDaysPerWeek this is the actual number of days.
    
    public int MappedValue { get; set; }

    public QuizQuestion? Question { get; set; }
}