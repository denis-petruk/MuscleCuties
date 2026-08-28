using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Repositories.Users;

public class UserRepository(AppDatabase db) : BaseRepository<User>(db), IUserRepository
{
    public new async Task<User?> GetByIdAsync(int id) =>
        await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByAppleUserIdAsync(string appleUserId) =>
        await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AppleUserId == appleUserId);

    public async Task<UserProfile?> GetProfileAsync(int userId) =>
        await _db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task AddProfileAsync(UserProfile profile)
    {
        await _db.UserProfiles.AddAsync(profile);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(UserProfile profile)
    {
        DetachTrackedLocal(profile);
        _db.UserProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }

    public async Task AddSnapshotAsync(UserProfileSnapshot snapshot)
    {
        await _db.UserProfileSnapshots.AddAsync(snapshot);
        await _db.SaveChangesAsync();
    }

    public async Task<UserProfileSnapshot?> GetLatestSnapshotAsync(int userId) =>
        await _db.UserProfileSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
}
