using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Registration.Integration.Tests;

internal static class SyntheticTenantSession
{
    public static async Task SetAsync(PostgresDbContext database, Guid userId)
    {
        await database.Database.OpenConnectionAsync();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.user_id', {userId.ToString()}, false)");
    }
}
