using MuscleCuties.Models;

namespace MuscleCuties.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);
    Task<User?> RegisterAsync(string email, string password);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
}