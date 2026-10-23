using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Profile;

public interface IProfileAccountService
{
    Task<User?> GetUserAsync(int userId);
    Task<UserProfile?> GetProfileAsync(int userId);
    Task SaveUserAsync(int userId, User user);
    Task SaveProfileAsync(int userId, UserProfile profile, bool isNewProfile);
    Task AddSnapshotAsync(int userId, UserProfileSnapshot snapshot);
    Task<string?> UpdateProfileImageAsync(int userId, string? imagePath);
}
