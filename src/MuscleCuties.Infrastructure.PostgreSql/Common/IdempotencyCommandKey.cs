using System.Text;

namespace MuscleCuties.Infrastructure.PostgreSql.Common;

/// <summary>
/// Idempotency keys are stored in a 128-character column. Short keys keep their readable prefix form;
/// longer ones are hashed so replay detection stays exact without truncation collisions.
/// </summary>
internal static class IdempotencyCommandKey
{
    /// <summary>A single key from a command family and its idempotency key, so two families cannot collide on one key.</summary>
    public static string Build(string prefix, string idempotencyKey)
    {
        var normalized = prefix + idempotencyKey.Trim();
        return normalized.Length <= 128
            ? normalized
            : prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey.Trim()))).ToLowerInvariant();
    }
}
