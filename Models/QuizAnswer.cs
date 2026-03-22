using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuscleCuties.Models;

public class QuizAnswer
{
    public int Id { get; set; }

    [Required] public string Text { get; set; } = null!; 

    public int OrderIndex { get; set; }

    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public QuizQuestion? Question { get; set; }
}