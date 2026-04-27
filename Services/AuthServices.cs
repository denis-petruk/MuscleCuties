using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Models;
using MuscleCuties.Repositories;

namespace MuscleCuties.Services;

public class AuthService(IUserRepository userRepository) : IAuthService
{
    private const string UserIdKey = "current_user_id";

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await userRepository.GetByEmailAsync(email);
        if (user == null) return null;

        return user.PasswordHash == HashPassword(password) ? user : null;
    }

    public async Task<User?> RegisterAsync(string email, string password)
    {
        var existing = await userRepository.GetByEmailAsync(email);
        if (existing != null) return null;

        var user = new User
        {
            Email = email,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsOnboardingComplete = false
        };

        await userRepository.AddAsync(user);
        await SecureStorage.SetAsync(UserIdKey, user.Id.ToString());
        return user;
    }

    public Task LogoutAsync()
    {
        SecureStorage.Remove(UserIdKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var id = await SecureStorage.GetAsync(UserIdKey);
        return id != null;
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        var id = await SecureStorage.GetAsync(UserIdKey);
        return id != null ? int.Parse(id) : 0;
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}