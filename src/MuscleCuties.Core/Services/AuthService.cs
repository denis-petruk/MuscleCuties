using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class AuthService : IAuthService
{
    private const string UserIdKey = "current_user_id";
    private readonly IUserRepository _userRepository;
    private readonly ISecureStorage _secureStorage;

    public AuthService(IUserRepository userRepository, ISecureStorage secureStorage)
    {
        _userRepository = userRepository;
        _secureStorage = secureStorage;
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null) return null;

        if (user.PasswordHash != HashPassword(password)) return null;

        await _secureStorage.SetAsync(UserIdKey, user.Id.ToString());
        return user;
    }

    public async Task<User?> RegisterAsync(string email, string password)
    {
        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing != null) return null;

        var user = new User
        {
            Email = email,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsOnboardingComplete = false
        };

        await _userRepository.AddAsync(user);
        await _secureStorage.SetAsync(UserIdKey, user.Id.ToString());
        return user;
    }

    public Task LogoutAsync()
    {
        _secureStorage.Remove(UserIdKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var id = await _secureStorage.GetAsync(UserIdKey);
        return id != null;
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        var id = await _secureStorage.GetAsync(UserIdKey);
        return id != null ? int.Parse(id) : 0;
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
