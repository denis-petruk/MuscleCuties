
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>A fingerprint of the inputs a plan depends on, which is how a cached or stored plan is proven current.</summary>
internal static class PlanningInputFingerprint
{
    /// <summary>The injury part of the fingerprint, ordered by a stable key so the hash does not depend on query order.</summary>
    public static string Injuries(IEnumerable<WorkoutInjuryLogRecord> injuries)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(injuries.OrderBy(item => item.Id).Select(item =>
            new
            {
                item.Id, item.SiteFlag, item.Status, item.Since, item.Pain, item.Date, item.ClearedDate,
                item.WorseningTrigger, item.ClinicianAdvisedAvoid
            }).ToArray());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>SHA-256 hash of the serialized inputs, used to detect changed planning inputs.</summary>
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
