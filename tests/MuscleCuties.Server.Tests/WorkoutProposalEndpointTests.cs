using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using MuscleCuties.Server;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class WorkoutProposalEndpointTests
{
    private const string SyntheticConnectionString =
        "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    private const string SigningKey = "synthetic-workout-proposal-endpoint-signing-key-for-tests-only";
    private const string Issuer = "SyntheticServer";
    private const string Audience = "SyntheticClient";

    [Theory]
    [InlineData("/api/v1/workouts/plan/strength-proposals")]
    [InlineData("/api/v1/workouts/plan/strength-proposals/00000000-0000-0000-0000-000000000001/accept")]
    public async Task ProposalMutationRoutesRejectAnonymousCallers(string route)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = SyntheticConnectionString,
            ["Authentication:SigningKey"] = SigningKey,
            ["Authentication:Issuer"] = Issuer,
            ["Authentication:Audience"] = Audience
        });

        await using var app = ApiApplication.Build(builder);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await app.StopAsync();
    }
}
