using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;
using MuscleCuties.Server;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;
using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// The automated simulator journey reads the registration code from
/// /api/v1/dev/email-challenge/latest instead of scraping process logs. That convenience must never
/// exist outside Development, so these tests pin both halves of the contract: the route plus the
/// in-memory hand-off in Development, and neither of them anywhere else.
/// </summary>
public sealed class DevelopmentVerificationCodeChannelTests
{
    private const string DevelopmentRoute = "/api/v1/dev/email-challenge/latest";
    private const int RegistrationPurpose = 1;
    private const string JourneyEmail = "journey@example.test";
    private const string JourneyCode = "426917";

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task VerificationCodeChannelIsAbsentOutsideDevelopment(string environmentName)
    {
        await using var host = await Host.StartAsync(environmentName);

        Assert.IsNotType<LocalDevelopmentEmailChallengeDelivery>(host.Delivery);

        var response = await host.Client.GetAsync(
            $"{DevelopmentRoute}?email={JourneyEmail}&purpose={RegistrationPurpose}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentRegistersTheChannelAndNeverServesACodeItDidNotGenerate()
    {
        await using var host = await Host.StartAsync("Development");

        Assert.IsType<LocalDevelopmentEmailChallengeDelivery>(host.Delivery);

        var empty = await host.Client.GetAsync(
            $"{DevelopmentRoute}?email=never-requested@example.test&purpose={RegistrationPurpose}");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);

        Assert.True(await host.Delivery.SendAsync(JourneyEmail, JourneyCode, RegistrationPurpose, default));

        var delivered = await host.Client.GetAsync(
            $"{DevelopmentRoute}?email=Journey@Example.Test&purpose={RegistrationPurpose}");
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        using var body = JsonDocument.Parse(await delivered.Content.ReadAsStringAsync());
        Assert.Equal(JourneyCode, body.RootElement.GetProperty("code").GetString());

        // The hand-off is scoped to the challenged address and purpose.
        var otherAddress = await host.Client.GetAsync(
            $"{DevelopmentRoute}?email=someone-else@example.test&purpose={RegistrationPurpose}");
        Assert.Equal(HttpStatusCode.NotFound, otherAddress.StatusCode);
        var otherPurpose = await host.Client.GetAsync(
            $"{DevelopmentRoute}?email={JourneyEmail}&purpose=2");
        Assert.Equal(HttpStatusCode.NotFound, otherPurpose.StatusCode);
    }

    private sealed class Host(WebApplication app, AsyncServiceScope scope, IEmailChallengeDelivery delivery)
        : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public IEmailChallengeDelivery Delivery { get; } = delivery;

        public static Task<Host> StartAsync(string environmentName) => StartCoreAsync(environmentName);

        private static async Task<Host> StartCoreAsync(string environmentName)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic",
                ["Authentication:SigningKey"] = "synthetic-dev-channel-test-signing-key-only",
                ["Authentication:Issuer"] = "synthetic-dev-channel-tests",
                ["Authentication:Audience"] = "synthetic-dev-channel-client"
            });
            var app = ApiApplication.Build(builder);
            await app.StartAsync();
            // The delivery is registered per scope, so resolve it from a live scope rather than the root.
            var scope = app.Services.CreateAsyncScope();
            var delivery = scope.ServiceProvider.GetRequiredService<IEmailChallengeDelivery>();
            return new Host(app, scope, delivery);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await scope.DisposeAsync();
            await app.DisposeAsync();
        }
    }
}
