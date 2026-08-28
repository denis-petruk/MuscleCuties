namespace MuscleCuties.App.Services.Health;

public sealed record WhoopOAuthOptions(
    string ClientId,
    string ClientSecret,
    Uri RedirectUri,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri ApiBaseUri,
    Uri? TokenExchangeEndpoint,
    string Scope)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && (TokenExchangeEndpoint is not null || !string.IsNullOrWhiteSpace(ClientSecret));

    public static WhoopOAuthOptions FromEnvironment()
    {
        var redirectUri = TryCreateUri(Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_REDIRECT_URI"))
                          ?? new Uri("musclecuties://whoop-callback");

        return new WhoopOAuthOptions(
            Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_CLIENT_ID") ?? string.Empty,
            Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_CLIENT_SECRET") ?? string.Empty,
            redirectUri,
            new Uri("https://api.prod.whoop.com/oauth/oauth2/auth"),
            new Uri("https://api.prod.whoop.com/oauth/oauth2/token"),
            new Uri("https://api.prod.whoop.com/"),
            TryCreateUri(Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_TOKEN_PROXY_URL")),
            Environment.GetEnvironmentVariable("MUSCLECUTIES_WHOOP_SCOPE")
            ?? "read:recovery read:cycles read:workout read:sleep read:profile read:body_measurement offline");
    }

    private static Uri? TryCreateUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
