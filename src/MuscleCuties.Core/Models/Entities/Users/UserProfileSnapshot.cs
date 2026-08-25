namespace MuscleCuties.Core.Models.Entities.Users;

public class UserProfileSnapshot
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string SnapshotReason { get; set; } = null!;
    public string ProfileJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}