using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed record CurrentUserState(int UserId, bool IsOnboardingComplete);

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);
    Task<User?> RegisterAsync(string email, string password);
    Task<bool> EmailExistsAsync(string email);
    Task<User?> SignInWithAppleAsync(AppleSignInResult appleAccount);
    Task<User?> SignInWithExternalProviderAsync(PlatformSignInResult account);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
    Task<CurrentUserState?> GetCurrentUserStateAsync();
}
