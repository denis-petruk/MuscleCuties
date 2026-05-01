using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<UserProfile?> GetProfileAsync(int userId);
    Task AddProfileAsync(UserProfile profile);
    Task UpdateProfileAsync(UserProfile profile);
    Task AddSnapshotAsync(UserProfileSnapshot snapshot);
    Task<UserProfileSnapshot?> GetLatestSnapshotAsync(int userId);
}