using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Cycle;

public sealed class CycleAccountService(IUserRepository users) : ICycleAccountService
{
    public async Task<DateTime?> GetAccountCreatedDateAsync(int userId)
    {
        var user = await users.GetByIdAsync(userId);
        return user?.CreatedAt.Date;
    }
}
