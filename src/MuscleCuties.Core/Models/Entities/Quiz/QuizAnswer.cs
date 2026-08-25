using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities.Quiz;

public class QuizAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    [Required] public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }
    // The value mapped to UserProfile or the profile snapshot when this answer is selected.
    // For enum-backed questions (Goal, DietaryPreference) this is the int cast of the enum value.
    // For phase symptom questions (Pain, Energy) this is a 1-5 scale.
    // For WorkoutDaysPerWeek this is the literal day count.
    public int MappedValue { get; set; }

    public QuizQuestion? Question { get; set; }
}
