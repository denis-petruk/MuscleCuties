using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<UserProfile?> GetProfileAsync(int userId);
    Task AddProfileAsync(UserProfile profile);
    Task UpdateProfileAsync(UserProfile profile);
    Task<UserBaselineProfile?> GetBaselineProfileAsync(int userId);
    Task AddBaselineProfileAsync(UserBaselineProfile baseline);
    Task UpdateBaselineProfileAsync(UserBaselineProfile baseline);
}
