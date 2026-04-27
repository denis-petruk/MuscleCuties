using System.ComponentModel.DataAnnotations.Schema;

namespace MuscleCuties.Core.Models.Entities;

public class UserQuizResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuestionId { get; set; }
    public int AnswerId { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))] public User? User { get; set; }
    [ForeignKey(nameof(QuestionId))] public QuizQuestion? Question { get; set; }
    [ForeignKey(nameof(AnswerId))] public QuizAnswer? Answer { get; set; }
}
