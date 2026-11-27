using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using MuscleCuties.Server;
using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// Pins that the Step 2 and Step 3 dead routes are no longer served by the API. Every row is an authenticated
/// route that used to answer 401 to an anonymous caller, so the assertion only passes once no handler
/// is registered for that verb and path.
/// </summary>
public sealed class DeadRouteSurfaceTests
{
    private const string SyntheticConnectionString =
        "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    private const string SigningKey = "synthetic-dead-route-signing-key-for-tests-only";
    private const string Issuer = "SyntheticServer";
    private const string Audience = "SyntheticClient";

    private const int NotFound = 404;

    // ASP.NET Core reports a method mismatch (405) rather than no-match (404) when another verb serves the
    // same path shape: POST /api/v1/nutrition/recipes/{anything} collides with the surviving
    // GET/PUT/DELETE /api/v1/nutrition/recipes/{id:guid} route, and the Guid constraint is not consulted
    // before that report (a path that never existed, e.g. /recipes/not-a-guid, also answers 405).
    // Both statuses prove no handler is registered for the asserted verb and path.
    private const int MethodNotAllowed = 405;

    [Theory]
    [InlineData("GET", "/api/v1/workouts/injuries/exercises/1/contraindicated", NotFound)]
    [InlineData("GET", "/api/v1/workouts/injuries/exercises/1/swaps", NotFound)]
    [InlineData("POST", "/api/v1/workouts/injuries/swap", NotFound)]
    [InlineData("GET", "/api/v1/workouts/history/personal-records", NotFound)]
    [InlineData("GET", "/api/v1/nutrition/diary/events", NotFound)]
    [InlineData("POST", "/api/v1/nutrition/diary/copy/preview", NotFound)]
    [InlineData("POST", "/api/v1/nutrition/diary/copy/commit", NotFound)]
    [InlineData("POST", "/api/v1/nutrition/diary/quick-add", NotFound)]
    [InlineData("POST", "/api/v1/nutrition/recipes/preview", MethodNotAllowed)]
    [InlineData("POST", "/api/v1/nutrition/recipes/00000000-0000-0000-0000-000000000001/portion-preview", NotFound)]
    [InlineData("GET", "/api/v1/cycle/history/prediction", NotFound)]
    [InlineData("DELETE", "/api/v1/cycle/history/cycles/00000000-0000-0000-0000-000000000001", NotFound)]
    [InlineData("POST", "/api/v1/nutrition/catalog/custom-foods", NotFound)]
    [InlineData("GET", "/api/v1/profile/history", NotFound)]
    [InlineData("GET", "/api/v1/profile/quiz-responses", NotFound)]
    [InlineData("PUT", "/api/v1/profile/quiz-responses/1", NotFound)]
    [InlineData("PUT", "/api/v1/profile/nutrition-settings", NotFound)]
    [InlineData("GET", "/api/v1/fasting/session", NotFound)]
    [InlineData("POST", "/api/v1/fasting/session/start", NotFound)]
    [InlineData("POST", "/api/v1/fasting/session/00000000-0000-0000-0000-000000000001/pause", NotFound)]
    [InlineData("POST", "/api/v1/fasting/session/00000000-0000-0000-0000-000000000001/resume", NotFound)]
    [InlineData("POST", "/api/v1/fasting/session/00000000-0000-0000-0000-000000000001/end", NotFound)]
    [InlineData("PUT", "/api/v1/fasting/session/00000000-0000-0000-0000-000000000001/correction", NotFound)]
    [InlineData("PUT", "/api/v1/fasting/session/00000000-0000-0000-0000-000000000001/planned-end", NotFound)]
    public async Task RemovedRouteIsNotServed(string method, string path, int expectedStatus)
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
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        await app.StopAsync();
    }
}
