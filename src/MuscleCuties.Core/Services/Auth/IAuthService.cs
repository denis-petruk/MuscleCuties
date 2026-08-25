using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);
    Task<User?> RegisterAsync(string email, string password);
    Task LogoutAsync();
    Task<bool> IsLoggedInAsync();
    Task<int> GetCurrentUserIdAsync();
}
