using System.ComponentModel.DataAnnotations;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Entities.Users;

/// <summary>An account: how it signs in, whether onboarding is done, and its profile.</summary>
public class User
{
    /// <summary>The stored identity, which the local rows are keyed by.</summary>
    public int Id { get; set; }
    [Required] public string Email { get; set; } = null!;
    /// <summary>The username, if the account has one; Apple sign-ins may not.</summary>
    public string? Username { get; set; }
    /// <summary>The Apple identifier, kept for accounts created through Apple.</summary>
    public string? AppleUserId { get; set; }
    /// <summary>How the account signs in, which decides whether a password hash is expected.</summary>
    public UserCredentialType CredentialType { get; set; } = UserCredentialType.Password;
    /// <summary>The hashed password, never the password itself, and absent for platform sign-ins.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the account last changed.</summary>
    public DateTime UpdatedAt { get; set; }
    /// <summary>Whether sign-in should lead to the dashboard or back into onboarding.</summary>
    public bool IsOnboardingComplete { get; set; }

    /// <summary>The profile, created once onboarding has answers to store.</summary>
    public UserProfile? UserProfile { get; set; }
}
