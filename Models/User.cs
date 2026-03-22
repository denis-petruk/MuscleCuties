using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Models;

public class User
{
    public int Id { get; set; }
    [Required] public string Email { get; set; } = null!;
    [Required] public string PasswordHash { get; set; } = null!;
    public string? FloAccessToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOnboardingComplete { get; set; }

    public UserProfile? UserProfile { get; set; }
    public UserBaselineProfile? UserBaselineProfile { get; set; }
}