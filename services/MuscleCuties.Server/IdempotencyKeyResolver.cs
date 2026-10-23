namespace MuscleCuties.Server;

/// <summary>Resolves the request key shared by nutrition and workout command endpoints.</summary>
public static class IdempotencyKeyResolver
{
    public const string HeaderName = "Idempotency-Key";

    public static string Resolve(HttpContext context, string? bodyKey)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            var headerKey = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerKey))
                return headerKey.Trim();
        }

        return bodyKey ?? string.Empty;
    }
}
