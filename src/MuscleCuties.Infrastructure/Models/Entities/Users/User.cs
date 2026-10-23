using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Entities.Users;

public class User
{
    public int Id { get; set; }
    [Required] public string Email { get; set; } = null!;
    public string? Username { get; set; }
    public string? AppleUserId { get; set; }
    public UserCredentialType CredentialType { get; set; } = UserCredentialType.Password;
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsOnboardingComplete { get; set; }

    public UserProfile? UserProfile { get; set; }
}
