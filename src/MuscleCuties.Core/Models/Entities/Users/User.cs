using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities.Users;

public class User
{
    public int Id { get; set; }
    [Required] public string Email { get; set; } = null!;
    [Required] public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsOnboardingComplete { get; set; }

    public UserProfile? UserProfile { get; set; }
}