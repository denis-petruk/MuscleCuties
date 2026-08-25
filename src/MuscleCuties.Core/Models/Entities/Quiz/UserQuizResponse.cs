using System.ComponentModel.DataAnnotations.Schema;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Models.Entities.Quiz;

public class UserQuizResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuizQuestionId { get; set; }
    public int QuizAnswerId { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    public int? UserProfileSnapshotId { get; set; }

    [ForeignKey(nameof(UserId))] public User? User { get; set; }
    [ForeignKey(nameof(QuizQuestionId))] public QuizQuestion? Question { get; set; }
    [ForeignKey(nameof(QuizAnswerId))] public QuizAnswer? Answer { get; set; }
    [ForeignKey(nameof(UserProfileSnapshotId))] public UserProfileSnapshot? Snapshot { get; set; }
}
