using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Models;

public class QuizQuestion
{
    public int Id { get; set; }
    [Required] public string Question { get; set; } = null!;
    public int OrderIndex { get; set; }
    public QuizQuestionType QuestionType { get; set; }

    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}