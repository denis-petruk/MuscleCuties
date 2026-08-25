using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Auth;

public class AuthService : IAuthService
{
    private const string UserIdKey = "current_user_id";

    private readonly IUserRepository _userRepository;
    private readonly ITokenStorage _tokenStorage;
    private readonly SemaphoreSlim _resolveUserLock = new(1, 1);
    private int? _cachedUserId;

    public AuthService(IUserRepository userRepository, ITokenStorage tokenStorage)
    {
        _userRepository = userRepository;
        _tokenStorage = tokenStorage;
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return null;

        if (user.PasswordHash != HashPassword(password))
            return null;

        await _tokenStorage.SetAsync(UserIdKey, user.Id.ToString());
        _cachedUserId = user.Id;
        return user;
    }

    public async Task<User?> RegisterAsync(string email, string password)
    {
        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing != null)
            return null;

        var user = new User
        {
            Email = email,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsOnboardingComplete = false
        };

        await _userRepository.AddAsync(user);
        await _tokenStorage.SetAsync(UserIdKey, user.Id.ToString());
        _cachedUserId = user.Id;

        return user;
    }

    public Task LogoutAsync()
    {
        _cachedUserId = null;
        _tokenStorage.Remove(UserIdKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var userId = await ResolveCurrentUserIdAsync();
        return userId > 0;
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        return await ResolveCurrentUserIdAsync();
    }

    private async Task<int> ResolveCurrentUserIdAsync()
    {
        if (_cachedUserId is int cachedUserId)
            return cachedUserId;

        await _resolveUserLock.WaitAsync();
        try
        {
            if (_cachedUserId is int resolvedUserId)
                return resolvedUserId;

            var id = await _tokenStorage.GetAsync(UserIdKey);
            if (string.IsNullOrWhiteSpace(id))
                return 0;

            if (!int.TryParse(id, out var userId) || userId <= 0)
            {
                _tokenStorage.Remove(UserIdKey);
                return 0;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                _cachedUserId = userId;
                return userId;
            }

            _tokenStorage.Remove(UserIdKey);
            return 0;
        }
        finally
        {
            _resolveUserLock.Release();
        }
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
