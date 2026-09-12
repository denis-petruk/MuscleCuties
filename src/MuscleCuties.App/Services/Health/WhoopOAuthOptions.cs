namespace MuscleCuties.App.Services.Health;

public sealed record WhoopOAuthOptions(
    string ClientId,
    Uri RedirectUri,
    Uri AuthorizationEndpoint,
    Uri ApiBaseUri,
    Uri? TokenExchangeEndpoint,
    string Scope)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && TokenExchangeEndpoint is not null;

    public static WhoopOAuthOptions FromEnvironment()
    {
        var redirectUri = TryCreateUri(Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_REDIRECT_URI"))
                          ?? new Uri("musclecuties://whoop-callback");

        return new WhoopOAuthOptions(
            Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_CLIENT_ID") ?? string.Empty,
            redirectUri,
            new Uri("https://api.prod.whoop.com/oauth/oauth2/auth"),
            new Uri("https://api.prod.whoop.com/"),
            TryCreateUri(Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_TOKEN_PROXY_URL")),
            Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_SCOPE")
            ?? "read:recovery read:cycles read:workout read:sleep read:profile read:body_measurement offline");
    }

    private static Uri? TryCreateUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }
}
