namespace MuscleCuties.UI.Services.Configuration;

/// <summary>Which backend a build talks to. Values are persisted in stored tokens and cache keys,</summary>
/// so they keep their historical numbers.</summary>
public enum AppTargetEnvironment
{
    Local = 0,
    // Beta keeps its historical value so no persisted environment identity shifts.
    Beta = 2
}

/// <summary>Decides the backend base URL and the storage keys that depend on it. Every URL passes</summary>
/// an explicit host allowlist, so a stored override cannot point a build at an unknown server.</summary>
public static class AppEndpointConfiguration
{
    public const string LocalBaseUrl = "http://127.0.0.1:5080";
    public const string BetaBaseUrl = "https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com";

    private static readonly HashSet<string> AllowlistedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.0.0.1",
        "localhost",
        "::1",
        "y2ejsgrc6l.execute-api.us-east-2.amazonaws.com"
    };

    public static AppTargetEnvironment ActiveEnvironment
    {
        get
        {
#if DEBUG
            return AppTargetEnvironment.Beta;
#elif APP_ENV_BETA
            return AppTargetEnvironment.Beta;
#else
            throw new InvalidOperationException(
                "Release builds must explicitly define APP_ENV_BETA. Implicit fallback to Beta is disabled for release safety.");
#endif
        }
    }

    /// <summary>The API base URL the environment talks to.</summary>
    public static string GetDefaultBaseUrl(AppTargetEnvironment environment) => environment switch
    {
        AppTargetEnvironment.Local => LocalBaseUrl,
        AppTargetEnvironment.Beta => BetaBaseUrl,
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unsupported target environment.")
    };

    /// <summary>Accepts only absolute http(s) URLs whose host is on the allowlist; returns the parsed</summary>
    /// URI so callers do not have to parse twice.</summary>
    public static bool IsAllowlisted(string urlString, out Uri? validatedUri)
    {
        validatedUri = null;
        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // DnsSafeHost strips IPv6 brackets ([::1] -> ::1) so the loopback entry can match.
        if (!AllowlistedHosts.Contains(uri.DnsSafeHost))
            return false;

        validatedUri = uri;
        return true;
    }

    /// <summary>
    /// A debug override may only move the local endpoint to another loopback port. A persisted
    /// override that names Beta (or any non-loopback host) is ignored, so a Debug build can never
    /// silently write synthetic test data into the shared Beta environment.
    /// </summary>
    public static bool IsLoopbackOverride(string? urlString, out Uri? validatedUri)
    {
        validatedUri = null;
        if (string.IsNullOrWhiteSpace(urlString) || !IsAllowlisted(urlString, out var uri) || uri is null)
            return false;

        if (!uri.IsLoopback)
            return false;

        validatedUri = uri;
        return true;
    }

    /// <summary>Honours an override only in a Local build and only when it stays on loopback, so a</summary>
    /// persisted override cannot redirect a debug build into a shared environment.</summary>
    public static string ResolveBackendBaseUrl(string? overrideUrl = null)
    {
        if (ActiveEnvironment == AppTargetEnvironment.Local &&
            IsLoopbackOverride(overrideUrl, out var validOverride))
        {
            return validOverride!.ToString().TrimEnd('/');
        }

        return GetDefaultBaseUrl(ActiveEnvironment);
    }

    /// <summary>The cache directory the environment uses.</summary>
    public static string GetCacheDirectoryName(AppTargetEnvironment environment) =>
        $"snapshots_{environment.ToString().ToLowerInvariant()}";

    /// <summary>The storage key that holds the environment's session token.</summary>
    public static string GetSessionTokenKey(AppTargetEnvironment environment) =>
        $"backend_session_token_{environment.ToString().ToLowerInvariant()}";
}
