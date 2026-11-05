namespace MuscleCuties.UI.Services.Configuration;

public enum AppTargetEnvironment
{
    Local = 0,
    Staging = 1,
    Beta = 2
}

public static class AppEndpointConfiguration
{
    public const string LocalBaseUrl = "http://127.0.0.1:5000";
    public const string StagingBaseUrl = "https://staging-api.musclecuties.app";
    public const string BetaBaseUrl = "https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com";

    private static readonly HashSet<string> AllowlistedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.0.0.1",
        "localhost",
        "::1",
        "staging-api.musclecuties.app",
        "y2ejsgrc6l.execute-api.us-east-2.amazonaws.com"
    };

    public static AppTargetEnvironment ActiveEnvironment
    {
        get
        {
#if DEBUG
            // Safe debug default is always Local to prevent accidental queries against shared data
            return AppTargetEnvironment.Local;
#elif APP_ENV_STAGING
            return AppTargetEnvironment.Staging;
#elif APP_ENV_BETA
            return AppTargetEnvironment.Beta;
#else
            throw new InvalidOperationException(
                "Release builds must explicitly define either APP_ENV_STAGING or APP_ENV_BETA. Implicit fallback to Beta is disabled for release safety.");
#endif
        }
    }

    public static string GetDefaultBaseUrl(AppTargetEnvironment environment) => environment switch
    {
        AppTargetEnvironment.Local => LocalBaseUrl,
        AppTargetEnvironment.Staging => StagingBaseUrl,
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

        if (!AllowlistedHosts.Contains(uri.Host))
            return false;

        validatedUri = uri;
        return true;
    }

    public static string ResolveBackendBaseUrl(string? overrideUrl = null)
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(overrideUrl) && IsAllowlisted(overrideUrl, out var validOverride))
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
