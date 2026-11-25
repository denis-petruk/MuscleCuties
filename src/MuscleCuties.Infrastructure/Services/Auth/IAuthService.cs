using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>The current account with whether it still has to finish onboarding.</summary>
public sealed record CurrentUserState(int UserId, bool IsOnboardingComplete);

/// <summary>The auth operations the pages call.</summary>
public interface IAuthService
{
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
    Task<CurrentUserState?> GetCurrentUserStateAsync();
}
