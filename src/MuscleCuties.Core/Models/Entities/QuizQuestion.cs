using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class QuizQuestion
{
    public int Id { get; set; }
    [Required] public string Question { get; set; } = null!;
    public int OrderIndex { get; set; }
    public QuizQuestionType QuestionType { get; set; }

    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}
