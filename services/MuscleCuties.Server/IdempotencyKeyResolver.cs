namespace MuscleCuties.Server;

/// <summary>Resolves the request key shared by nutrition and workout command endpoints.</summary>
public static class IdempotencyKeyResolver
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>U-08: header sent by mobile clients released before the standard name was adopted.</summary>
    public const string LegacyHeaderName = "X-Idempotency-Key";

    public static string Resolve(HttpContext context, string? bodyKey)
    {
        foreach (var name in new[] { HeaderName, LegacyHeaderName })
        {
            if (!context.Request.Headers.TryGetValue(name, out var headerValues))
                continue;
            var headerKey = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerKey))
                return headerKey.Trim();
        }

        return bodyKey ?? string.Empty;
    }
}
