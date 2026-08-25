using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Repositories.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<UserProfile?> GetProfileAsync(int userId);
    Task AddProfileAsync(UserProfile profile);
    Task UpdateProfileAsync(UserProfile profile);
    Task AddSnapshotAsync(UserProfileSnapshot snapshot);
    Task<UserProfileSnapshot?> GetLatestSnapshotAsync(int userId);
}
