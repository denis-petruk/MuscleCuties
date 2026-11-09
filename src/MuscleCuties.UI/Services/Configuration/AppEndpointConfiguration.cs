namespace MuscleCuties.UI.Services.Configuration;

public enum AppTargetEnvironment
{
    Local = 0,
    // Beta keeps its historical value so no persisted environment identity shifts.
    Beta = 2
}

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
            // Safe debug default is always Local to prevent accidental queries against shared data
            return AppTargetEnvironment.Local;
#elif APP_ENV_BETA
            return AppTargetEnvironment.Beta;
#else
            throw new InvalidOperationException(
                "Release builds must explicitly define APP_ENV_BETA. Implicit fallback to Beta is disabled for release safety.");
#endif
        }
    }

    public static string GetDefaultBaseUrl(AppTargetEnvironment environment) => environment switch
    {
        AppTargetEnvironment.Local => LocalBaseUrl,
        AppTargetEnvironment.Beta => BetaBaseUrl,
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unsupported target environment.")
    };

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

    public static string ResolveBackendBaseUrl(string? overrideUrl = null)
    {
#if DEBUG
        if (IsLoopbackOverride(overrideUrl, out var validOverride))
        {
            return validOverride!.ToString().TrimEnd('/');
        }
#endif
        return GetDefaultBaseUrl(ActiveEnvironment);
    }

    public static string GetCacheDirectoryName(AppTargetEnvironment environment) =>
        $"snapshots_{environment.ToString().ToLowerInvariant()}";

    public static string GetSessionTokenKey(AppTargetEnvironment environment) =>
        $"backend_session_token_{environment.ToString().ToLowerInvariant()}";
}
