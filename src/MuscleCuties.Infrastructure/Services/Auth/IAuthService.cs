using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed record CurrentUserState(int UserId, bool IsOnboardingComplete);

public sealed record PasswordOperationResult(bool Succeeded, string Message = "");

public sealed record AppleSignInOutcome(
    bool Succeeded,
    bool IsExistingUser,
    User? User,
    bool IsPendingOnboarding,
    string? ErrorMessage = null);

public interface IAuthService
{
    Task<User?> LoginAsync(string emailOrUsername, string password);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task<User?> SignInWithAppleAsync(AppleSignInResult appleAccount);
    Task<AppleSignInOutcome> HandleAppleSignInAsync(AppleSignInResult appleAccount);
    Task<User?> SignInWithExternalProviderAsync(PlatformSignInResult account);
    Task<PasswordOperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
    Task<CurrentUserState?> GetCurrentUserStateAsync();
    Task SetCurrentUserAsync(User user);
}
