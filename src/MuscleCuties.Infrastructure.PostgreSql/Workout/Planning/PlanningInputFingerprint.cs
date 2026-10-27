using System.Security.Cryptography;
using System.Text.Json;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal static class PlanningInputFingerprint
{
    public static string Injuries(IEnumerable<WorkoutInjuryLogRecord> injuries)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(injuries.OrderBy(item => item.Id).Select(item =>
            new { item.Id, item.SiteFlag, item.Status, item.Since, item.Pain, item.Date, item.ClearedDate }).ToArray());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
