using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Profile;

public sealed class ProfileAccountService(IUserRepository users) : IProfileAccountService
{
    public Task<User?> GetUserAsync(int userId) => users.GetByIdAsync(userId);

    public Task<UserProfile?> GetProfileAsync(int userId) => users.GetProfileAsync(userId);

    public Task SaveUserAsync(int userId, User user)
    {
        EnsureUser(userId, user.Id);
        return users.UpdateAsync(user);
    }

    public Task SaveProfileAsync(int userId, UserProfile profile, bool isNewProfile)
    {
        EnsureUser(userId, profile.UserId);
        return isNewProfile ? users.AddProfileAsync(profile) : users.UpdateProfileAsync(profile);
    }

    public Task AddSnapshotAsync(int userId, UserProfileSnapshot snapshot)
    {
        EnsureUser(userId, snapshot.UserId);
        return users.AddSnapshotAsync(snapshot);
    }

    public async Task<string?> UpdateProfileImageAsync(int userId, string? imagePath)
    {
        var profile = await users.GetProfileAsync(userId);
        if (profile is null)
            return null;

        profile.ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
        await users.UpdateProfileAsync(profile);
        return profile.ProfileImagePath;
    }

    private static void EnsureUser(int expectedUserId, int actualUserId)
    {
        if (expectedUserId <= 0 || expectedUserId != actualUserId)
            throw new InvalidOperationException("Profile data must belong to the current user.");
    }
}
