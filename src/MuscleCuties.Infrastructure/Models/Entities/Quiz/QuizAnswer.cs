using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities.Quiz;

/// <summary>One answer option of a quiz question, with the value it maps to in the profile.</summary>
public class QuizAnswer
{
    /// <summary>The stored identity.</summary>
    public int Id { get; set; }
    /// <summary>The question it answers.</summary>
    public int QuestionId { get; set; }
    [Required] public string Text { get; set; } = null!;
    /// <summary>Where it sits in the list of options.</summary>
    public int OrderIndex { get; set; }
    /// <summary>The number the answer stores in the profile, which is what makes the quiz data rather than text.</summary>
    public int MappedValue { get; set; }

    /// <summary>The navigation to the question.</summary>
    public QuizQuestion? Question { get; set; }
}
