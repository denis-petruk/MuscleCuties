using System.ComponentModel.DataAnnotations;
using MuscleCuties.Models;
using MuscleCuties.Models.Enums;

public class QuizQuestion
{
    public int Id { get; set; }
    [Required] public string Question { get; set; } = null!;
    public int OrderIndex { get; set; }
    public CyclePhase? Phase { get; set; }

    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}