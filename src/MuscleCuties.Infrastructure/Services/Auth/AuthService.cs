using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Auth;

public class AuthService : IAuthService
{
    private const string UserIdKey = "current_user_id";
    private const long UncachedSessionVersion = -1;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPendingRegistrationService? _pendingRegistrationService;
    private readonly AccountSessionGate _sessionGate;
    private readonly ITokenStorage _tokenStorage;
    private readonly IUserRepository _userRepository;
    private CurrentUserState? _cachedSession;
    private long _cachedSessionVersion = UncachedSessionVersion;

    public AuthService(
        IUserRepository userRepository,
        ITokenStorage tokenStorage,
        IPasswordHasher? passwordHasher = null,
        IPendingRegistrationService? pendingRegistrationService = null,
        AccountSessionGate? sessionGate = null)
    {
        _userRepository = userRepository;
        _tokenStorage = tokenStorage;
        _passwordHasher = passwordHasher ?? PasswordHasher.Default;
        _pendingRegistrationService = pendingRegistrationService;
        _sessionGate = sessionGate ?? AccountSessionGate.Shared;
    }

    public async Task<User?> LoginAsync(string emailOrUsername, string password)
    {
        if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
            return null;

        var trimmedIdentifier = emailOrUsername.Trim();
        User? user = null;

        if (trimmedIdentifier.Contains('@'))
        {
            var normalizedEmail = NormalizeEmail(trimmedIdentifier);
            user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null) user = await _userRepository.GetByUsernameAsync(trimmedIdentifier);
        }
        else
        {
            user = await _userRepository.GetByUsernameAsync(trimmedIdentifier);
            if (user == null) user = await _userRepository.GetByEmailAsync(NormalizeEmail(trimmedIdentifier));
        }

        if (user == null) return null;

        // Apple-only accounts have no local password credential.
        if (user.CredentialType == UserCredentialType.Apple || string.IsNullOrWhiteSpace(user.PasswordHash))
            return null;

        var (isValid, needsMigration) = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid) return null;

        if (needsMigration)
        {
            var originalHash = user.PasswordHash;
            try
            {
                user.PasswordHash = _passwordHasher.HashPassword(password);
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AuthService] Credential migration failed ({ex.GetType().Name}).");
                user.PasswordHash = originalHash;
            }
        }

        if (!await TrySetCurrentUserAsync(user))
            return null;

        _pendingRegistrationService?.Clear();
        return user;
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? Task.FromResult(false)
            : _userRepository.EmailExistsAsync(normalizedEmail);
    }

    public Task<bool> UsernameExistsAsync(string username)
    {
        var normalized = username?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? Task.FromResult(false)
            : _userRepository.UsernameExistsAsync(normalized);
    }

    public async Task<User?> SignInWithAppleAsync(AppleSignInResult appleAccount)
    {
        var outcome = await HandleAppleSignInAsync(appleAccount);
        return outcome.User;
    }

    public async Task<AppleSignInOutcome> HandleAppleSignInAsync(AppleSignInResult appleAccount)
    {
        if (string.IsNullOrWhiteSpace(appleAccount.UserIdentifier))
            return new AppleSignInOutcome(false, false, null, false, "Apple account information was not received.");

        var appleUserId = appleAccount.UserIdentifier.Trim();
        var existingAppleUser = await _userRepository.GetByAppleUserIdAsync(appleUserId);

        if (existingAppleUser is not null)
        {
            if (!await TrySetCurrentUserAsync(existingAppleUser))
                return new AppleSignInOutcome(false, false, null, false, "Account not found.");
            return new AppleSignInOutcome(true, true, existingAppleUser, false);
        }

        // Guard: check if an existing local account already uses this email.
        // Prevent silent takeover by an unlinked provider identity.
        if (!string.IsNullOrWhiteSpace(appleAccount.Email))
        {
            var normalizedEmail = NormalizeEmail(appleAccount.Email);
            var accountWithEmail = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (accountWithEmail is not null)
                return new AppleSignInOutcome(
                    false,
                    false,
                    null,
                    false,
                    "An account with this email already exists. Log in with your password to access your account.");
        }

        // New Apple identity: do NOT write a User row yet.
        // Stage pending Apple registration and defer local User creation until onboarding commit.
        var email = BuildAppleEmail(appleUserId, appleAccount.Email);
        var suggestedUsername = DeriveSuggestedUsername(appleAccount.FullName, appleUserId);

        _pendingRegistrationService?.StartAppleRegistration(appleUserId, email, suggestedUsername);

        return new AppleSignInOutcome(true, false, null, true);
    }

    public async Task<User?> SignInWithExternalProviderAsync(PlatformSignInResult account)
    {
        var provider = NormalizeProvider(account.Provider);
        if (provider == "apple")
        {
            var outcome = await HandleAppleSignInAsync(new AppleSignInResult(
                account.UserIdentifier,
                account.Email,
                account.FullName));
            return outcome.User;
        }

        if (string.IsNullOrWhiteSpace(account.UserIdentifier)) return null;

        var normalizedEmail = NormalizeEmail(account.Email);
        var user = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await _userRepository.GetByEmailAsync(normalizedEmail);

        if (user is not null && !await TrySetCurrentUserAsync(user))
            return null;

        return user;
    }

    public async Task<PasswordOperationResult> ChangePasswordAsync(int userId, string currentPassword,
        string newPassword)
    {
        if (userId <= 0)
            return new PasswordOperationResult(false, "Active user session not found.");

        if (string.IsNullOrWhiteSpace(currentPassword))
            return new PasswordOperationResult(false, "Please enter your current password.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return new PasswordOperationResult(false, "Account not found.");

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return new PasswordOperationResult(false, "This account uses device sign in and does not have a password.");

        var (isValid, _) = _passwordHasher.VerifyPassword(currentPassword, user.PasswordHash);
        if (!isValid)
            return new PasswordOperationResult(false, "Current password is incorrect.");

        return await ApplyNewPasswordAsync(user, newPassword, "Password updated.");
    }

    public async Task LogoutAsync()
    {
        using var lease = await _sessionGate.EnterAsync().ConfigureAwait(false);
        _cachedSession = null;
        try
        {
            _tokenStorage.Remove(UserIdKey);
        }
        finally
        {
            _sessionGate.MarkChanged();
            _cachedSessionVersion = UncachedSessionVersion;
        }
        if (await _tokenStorage.GetAsync(UserIdKey) is not null)
            throw new InvalidOperationException("The local session could not be cleared.");
        _cachedSessionVersion = _sessionGate.Version;
        _pendingRegistrationService?.Clear();
    }

    public async Task<bool> IsLoggedInAsync()
    {
        return await ResolveCurrentSessionAsync() is not null;
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        return (await ResolveCurrentSessionAsync())?.UserId ?? 0;
    }

    public Task<CurrentUserState?> GetCurrentUserStateAsync()
    {
        return ResolveCurrentSessionAsync();
    }

    public async Task SetCurrentUserAsync(User user)
    {
        if (!await TrySetCurrentUserAsync(user))
            throw new InvalidOperationException("The local account no longer exists.");
    }

    private async Task<bool> TrySetCurrentUserAsync(User user)
    {
        while (true)
        {
            var observedVersion = _sessionGate.Version;
            // Check SQLite before entering the session gate. Account purge takes
            // the SQLite transaction first, so this preserves the lock order.
            var isOnboardingComplete = await _userRepository.GetOnboardingCompletionAsync(user.Id);
            if (isOnboardingComplete is null)
                return false;

            using var lease = await _sessionGate.EnterAsync().ConfigureAwait(false);
            if (observedVersion != _sessionGate.Version)
                continue;

            try
            {
                await _tokenStorage.SetAsync(UserIdKey, user.Id.ToString());
            }
            finally
            {
                _sessionGate.MarkChanged();
                _cachedSession = null;
                _cachedSessionVersion = UncachedSessionVersion;
            }

            if (await _tokenStorage.GetAsync(UserIdKey) != user.Id.ToString())
                throw new InvalidOperationException("The local session could not be saved.");

            _cachedSession = new CurrentUserState(user.Id, isOnboardingComplete.Value);
            _cachedSessionVersion = _sessionGate.Version;
            return true;
        }
    }

    private async Task<CurrentUserState?> ResolveCurrentSessionAsync()
    {
        while (true)
        {
            int userId;
            long observedVersion;
            using (var lease = await _sessionGate.EnterAsync().ConfigureAwait(false))
            {
                if (_cachedSessionVersion == _sessionGate.Version)
                    return _cachedSession;

                var id = await _tokenStorage.GetAsync(UserIdKey);
                if (string.IsNullOrWhiteSpace(id))
                {
                    _cachedSession = null;
                    _cachedSessionVersion = _sessionGate.Version;
                    return null;
                }

                if (!int.TryParse(id, out userId) || userId <= 0)
                {
                    try
                    {
                        _tokenStorage.Remove(UserIdKey);
                    }
                    finally
                    {
                        _sessionGate.MarkChanged();
                        _cachedSession = null;
                        _cachedSessionVersion = UncachedSessionVersion;
                    }

                    return null;
                }

                observedVersion = _sessionGate.Version;
            }

            // A repository may wait on SQLite. Never hold the session gate
            // across that query: database mutations acquire SQLite first.
            var isOnboardingComplete = await _userRepository.GetOnboardingCompletionAsync(userId);

            using (var lease = await _sessionGate.EnterAsync().ConfigureAwait(false))
            {
                if (observedVersion != _sessionGate.Version)
                    continue;

                if (isOnboardingComplete is { } completed)
                {
                    _cachedSession = new CurrentUserState(userId, completed);
                    _cachedSessionVersion = observedVersion;
                    return _cachedSession;
                }

                try
                {
                    _tokenStorage.Remove(UserIdKey);
                }
                finally
                {
                    _sessionGate.MarkChanged();
                    _cachedSession = null;
                    _cachedSessionVersion = UncachedSessionVersion;
                }

                return null;
            }
        }
    }

    private async Task<PasswordOperationResult> ApplyNewPasswordAsync(
        User user,
        string newPassword,
        string successMessage)
    {
        if (!AuthInputValidator.IsStrongPassword(newPassword))
            return new PasswordOperationResult(false, AuthInputValidator.PasswordRequirementsMessage);

        var (isSame, _) = _passwordHasher.VerifyPassword(newPassword, user.PasswordHash);
        if (isSame)
            return new PasswordOperationResult(false, "New password must be different from your current password.");

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return new PasswordOperationResult(true, successMessage);
    }

    public static string HashPassword(string password)
    {
        return PasswordHasher.Default.HashPassword(password);
    }

    public static (bool IsValid, bool NeedsMigration) VerifyPassword(string password, string? storedHash)
    {
        return PasswordHasher.Default.VerifyPassword(password, storedHash);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "external"
            : provider.Trim().ToLowerInvariant();
    }

    private static string BuildAppleEmail(string appleUserId, string? email)
    {
        return BuildSyntheticEmail(email, "apple", appleUserId, "privaterelay.musclecuties.local");
    }

    private static string BuildSyntheticEmail(string? email, string prefix, string seed, string domain)
    {
        if (!string.IsNullOrWhiteSpace(email))
            return NormalizeEmail(email);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var suffix = Convert.ToHexString(bytes)[..20].ToLowerInvariant();
        return $"{prefix}-{suffix}@{domain}";
    }

    private static string DeriveSuggestedUsername(string? fullName, string appleUserId)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var cleaned = string.Concat(fullName.Where(c => char.IsLetterOrDigit(c) || c is '_' or '.'));
            if (cleaned.Length >= 3)
                return cleaned.Length > 30 ? cleaned[..30] : cleaned;
        }

        var suffix = appleUserId.Length >= 6 ? appleUserId[^6..] : appleUserId;
        var safeSuffix = string.Concat(suffix.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return $"apple_user_{safeSuffix}";
    }
}
