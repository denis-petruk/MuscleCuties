using MuscleCuties.UI.Services.Configuration;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class AppEndpointConfigurationTests
{
    [Fact]
    public void Endpoint_Urls_Match_Documented_Targets()
    {
        Assert.Equal("http://127.0.0.1:5080", AppEndpointConfiguration.LocalBaseUrl);
        Assert.Equal("https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com", AppEndpointConfiguration.BetaBaseUrl);
    }

    [Fact]
    public void No_Staging_Target_Exists()
    {
        // Beta is the only non-local target; a Staging host must never pass the allowlist.
        Assert.False(AppEndpointConfiguration.IsAllowlisted("https://staging-api.musclecuties.app", out _));
        Assert.DoesNotContain("Staging", Enum.GetNames<AppTargetEnvironment>());
    }

    [Theory]
    [InlineData("http://127.0.0.1:5080", true)]
    [InlineData("http://localhost:5080", true)]
    [InlineData("https://staging-api.musclecuties.app", false)]
    [InlineData("https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com", true)]
    [InlineData("https://malicious.example.com", false)]
    [InlineData("ftp://127.0.0.1:5000", false)]
    [InlineData("not-a-url", false)]
    public void Allowlist_Only_Permits_Known_Endpoints(string url, bool expectedAllowed)
    {
        var allowed = AppEndpointConfiguration.IsAllowlisted(url, out _);
        Assert.Equal(expectedAllowed, allowed);
    }

    [Fact]
    public void Cache_And_Token_Keys_Are_Partitioned_By_Environment()
    {
        var localDir = AppEndpointConfiguration.GetCacheDirectoryName(AppTargetEnvironment.Local);
        var betaDir = AppEndpointConfiguration.GetCacheDirectoryName(AppTargetEnvironment.Beta);

        Assert.NotEqual(localDir, betaDir);

        var localKey = AppEndpointConfiguration.GetSessionTokenKey(AppTargetEnvironment.Local);
        var betaKey = AppEndpointConfiguration.GetSessionTokenKey(AppTargetEnvironment.Beta);

        Assert.NotEqual(localKey, betaKey);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5090", true)]
    [InlineData("http://localhost:5090", true)]
    [InlineData("http://[::1]:5090", true)]
    [InlineData("https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com", false)]
    [InlineData("https://malicious.example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Debug_Override_Accepts_Only_Loopback_Hosts(string? url, bool expected)
    {
        // A persisted debug override naming Beta must not route a Debug build to shared data.
        Assert.Equal(expected, AppEndpointConfiguration.IsLoopbackOverride(url, out _));
    }

    [Fact]
    public void Debug_Build_Ignores_A_Persisted_Beta_Override()
    {
#if DEBUG
        Assert.Equal(
            AppEndpointConfiguration.LocalBaseUrl,
            AppEndpointConfiguration.ResolveBackendBaseUrl(AppEndpointConfiguration.BetaBaseUrl));
        Assert.Equal(
            "http://127.0.0.1:5090",
            AppEndpointConfiguration.ResolveBackendBaseUrl("http://127.0.0.1:5090/"));
#endif
    }
}
