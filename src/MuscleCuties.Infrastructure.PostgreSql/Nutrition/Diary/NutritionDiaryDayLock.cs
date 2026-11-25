
namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;

/// <summary>Serialises writes to one account's day with a row lock, so two entries cannot both compute the day's totals from the same stale read.</summary>
internal static class NutritionDiaryDayLock
{
    /// <summary>Takes the day's advisory lock and returns its key so the caller can release it on the same connection.</summary>
    internal static Task<int> AcquireAsync(PostgresDbContext context, Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var key = $"{userId:D}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
