using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;

internal static class NutritionDiaryDayLock
{
    internal static Task<int> AcquireAsync(PostgresDbContext context, Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var key = $"{userId:D}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
