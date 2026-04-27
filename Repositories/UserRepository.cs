using Microsoft.EntityFrameworkCore;
using MuscleCuties.Data;
using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

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

    public async Task<UserBaselineProfile?> GetBaselineProfileAsync(int userId) =>
        await _db.UserBaselineProfiles.FirstOrDefaultAsync(b => b.UserId == userId);

    public async Task AddBaselineProfileAsync(UserBaselineProfile baseline)
    {
        await _db.UserBaselineProfiles.AddAsync(baseline);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateBaselineProfileAsync(UserBaselineProfile baseline)
    {
        _db.UserBaselineProfiles.Update(baseline);
        await _db.SaveChangesAsync();
    }
}