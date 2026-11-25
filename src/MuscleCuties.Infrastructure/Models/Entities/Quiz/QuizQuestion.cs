using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Core.Models.Entities.Quiz;

/// <summary>One quiz question and the answers it offers.</summary>
public class QuizQuestion
{
    /// <summary>The stored identity.</summary>
    public int Id { get; set; }
    [Required] public string Question { get; set; } = null!;
    /// <summary>Where the question comes in the quiz.</summary>
    public int OrderIndex { get; set; }
    /// <summary>Which profile field the answer feeds, which is what decides how the question is drawn.</summary>
    public QuizQuestionType QuestionType { get; set; }

    /// <summary>The options offered.</summary>
    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}
