using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities.Quiz;

public class QuizAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    [Required] public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }
    public int MappedValue { get; set; }

    public QuizQuestion? Question { get; set; }
}
