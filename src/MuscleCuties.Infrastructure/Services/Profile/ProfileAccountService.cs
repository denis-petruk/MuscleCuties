using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Profile;

public sealed class ProfileAccountService(IUserRepository users, AppDatabase database) : IProfileAccountService
{
    public Task<User?> GetUserAsync(int userId)
    {
        return users.GetByIdAsync(userId);
    }

    public Task<UserProfile?> GetProfileAsync(int userId)
    {
        return users.GetProfileAsync(userId);
    }

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

        var oldPath = profile.ProfileImagePath;
        var newPath = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();

        if (!string.IsNullOrWhiteSpace(oldPath) &&
            !string.Equals(ProfileMediaPaths.Normalize(oldPath), ProfileMediaPaths.Normalize(newPath),
                StringComparison.Ordinal))
            await ProfileMediaDeletion.DeleteIfUnreferencedAsync(database, oldPath, excludedProfileUserId: userId);

        profile.ProfileImagePath = newPath;
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
