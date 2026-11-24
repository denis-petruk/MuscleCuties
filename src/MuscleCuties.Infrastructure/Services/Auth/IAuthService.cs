using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed record CurrentUserState(int UserId, bool IsOnboardingComplete);

public interface IAuthService
{
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
    Task<CurrentUserState?> GetCurrentUserStateAsync();
}
