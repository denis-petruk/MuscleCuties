using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class UserRepository(AppDatabase db) : BaseRepository<User>(db), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<UserProfile?> GetProfileAsync(int userId) =>
        await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task AddProfileAsync(UserProfile profile)
    {
        await _db.UserProfiles.AddAsync(profile);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateProfileAsync(UserProfile profile)
    {
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
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
}