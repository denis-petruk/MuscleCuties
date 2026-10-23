using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MuscleCuties.Server;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class RegistrationAuthenticationRouteTests
{
    private const string RegistrationRoute = "/api/v1/accounts/registration";
    private const string PasswordRoute = "/api/v1/auth/password";
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private const string SyntheticEmail = "registration@example.test";
    private const string SyntheticUsername = "syntheticregistration";
    private const string SyntheticPassword = "SyntheticRegistration123!";
    private const string SyntheticReceipt = "synthetic-receipt";
    private const string SyntheticFailureDetail = "Synthetic internal validation detail";
    private const string SyntheticRefreshToken = "synthetic-refresh";
    private const int SyntheticCycleDays = 28;
    private const int ConflictingCycleDays = 29;
    private const int SyntheticWorkoutDays = 3;
    private const int SyntheticActivity = 1;
    private const int SyntheticHeightCm = 170;
    private const int SyntheticWeightKg = 70;
    private const int SyntheticQuestionId = 1;
    private const int SyntheticAnswerId = 1;
    private const int SyntheticBirthYear = 1996;
    private const int SyntheticBirthMonth = 4;
    private const int SyntheticBirthDay = 12;
    private static readonly Guid SyntheticUserId = Guid.Parse("a3bef469-af83-48c3-b19f-710174691261");

    [Fact]
    public async Task MobileRegistrationPayloadPreservesProfileAndReceiptWithoutClientPlan()
    {
        await using var fixture = await Fixture.StartAsync();

        var response = await fixture.Client.PostAsJsonAsync(RegistrationRoute, MobileRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var request = Assert.IsType<CompleteAccountRegistrationRequest>(fixture.Registration.LastRequest);
        Assert.Equal(SyntheticCycleDays, request.Profile.CycleLength);
        Assert.Equal(SyntheticHeightCm, request.Profile.HeightCm);
        Assert.Equal(SyntheticWeightKg, request.Profile.WeightKg);
        Assert.Equal(new DateTime(SyntheticBirthYear, SyntheticBirthMonth, SyntheticBirthDay), request.Profile.BirthDate);
        Assert.Equal(SyntheticReceipt, request.EmailVerificationToken);
        Assert.Equal(SyntheticUserId.ToString("D"), request.EmailChallengeId);
        Assert.True(request.PrivacyConsent?.Accepted);
        Assert.Null(request.InitialWorkoutPlanDays);
        Assert.Null(request.InitialWorkoutPlanName);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(SyntheticUserId, body.GetProperty("userId").GetGuid());
        Assert.True(body.GetProperty("onboardingComplete").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingCycleLengthFieldRemainsCompatible(bool includeMobileField)
    {
        await using var fixture = await Fixture.StartAsync();
        var payload = JsonSerializer.SerializeToNode(MobileRequest())!;
        var profile = payload["profile"]!.AsObject();
        profile["cycleLength"] = SyntheticCycleDays;
        if (!includeMobileField)
            profile.Remove("cycleLengthDays");

        var response = await fixture.Client.PostAsJsonAsync(RegistrationRoute, payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(SyntheticCycleDays, fixture.Registration.LastRequest?.Profile.CycleLength);
    }

    [Fact]
    public async Task ConflictingCycleLengthFieldsAreRejectedBeforeRegistration()
    {
        await using var fixture = await Fixture.StartAsync();
        var payload = JsonSerializer.SerializeToNode(MobileRequest())!;
        payload["profile"]!["cycleLength"] = ConflictingCycleDays;

        var response = await fixture.Client.PostAsJsonAsync(RegistrationRoute, payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fixture.Registration.LastRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("conflictingCycleLengths", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(CompleteAccountRegistrationStatus.InvalidEmailVerification, HttpStatusCode.Unauthorized)]
    [InlineData(CompleteAccountRegistrationStatus.PrivacyConsentRequired, HttpStatusCode.BadRequest)]
    [InlineData(CompleteAccountRegistrationStatus.InvalidProfile, HttpStatusCode.BadRequest)]
    [InlineData(CompleteAccountRegistrationStatus.InvalidQuiz, HttpStatusCode.BadRequest)]
    [InlineData(CompleteAccountRegistrationStatus.EmailAlreadyRegistered, HttpStatusCode.Conflict)]
    [InlineData(CompleteAccountRegistrationStatus.UsernameTaken, HttpStatusCode.Conflict)]
    [InlineData(CompleteAccountRegistrationStatus.EmailReceiptAlreadyUsed, HttpStatusCode.Conflict)]
    public async Task RegistrationMapsApplicationFailures(CompleteAccountRegistrationStatus status, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.StartAsync();
        fixture.Registration.Status = status;

        var response = await fixture.Client.PostAsJsonAsync(RegistrationRoute, MobileRequest());

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationValidationUsesStableStatusCodeInsteadOfFreeFormDetail()
    {
        await using var fixture = await Fixture.StartAsync();
        fixture.Registration.Status = CompleteAccountRegistrationStatus.InvalidQuiz;
        fixture.Registration.ErrorMessage = SyntheticFailureDetail;

        var response = await fixture.Client.PostAsJsonAsync(RegistrationRoute, MobileRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(nameof(CompleteAccountRegistrationStatus.InvalidQuiz), body.GetProperty("code").GetString());
        Assert.DoesNotContain(SyntheticFailureDetail, body.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordLoginAndRefreshAcceptMobilePayloadAndReturnSessionEnvelope()
    {
        await using var fixture = await Fixture.StartAsync();

        var login = await fixture.Client.PostAsJsonAsync(PasswordRoute,
            new { emailOrUsername = SyntheticEmail, password = SyntheticPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(new PasswordLoginRequest(SyntheticEmail, SyntheticPassword), fixture.Authentication.LastLogin);
        var session = await login.Content.ReadFromJsonAsync<SessionTokens>();
        Assert.NotNull(session);
        Assert.Equal(SyntheticUserId, session.UserId);

        var refresh = await fixture.Client.PostAsJsonAsync(RefreshRoute, new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(new RefreshSessionRequest(SyntheticRefreshToken), fixture.Authentication.LastRefresh);
        Assert.Equal(session, await refresh.Content.ReadFromJsonAsync<SessionTokens>());
    }

    [Theory]
    [InlineData(PasswordRoute)]
    [InlineData(RefreshRoute)]
    public async Task InvalidCredentialsDoNotReturnSessionTokens(string route)
    {
        await using var fixture = await Fixture.StartAsync();
        fixture.Authentication.Status = SessionOperationStatus.InvalidCredentials;

        var response = await fixture.Client.PostAsJsonAsync(route,
            new { emailOrUsername = SyntheticEmail, password = SyntheticPassword, refreshToken = SyntheticRefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(SyntheticRefreshToken, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static object MobileRequest() => new
    {
        email = SyntheticEmail,
        username = SyntheticUsername,
        password = SyntheticPassword,
        emailVerificationToken = SyntheticReceipt,
        emailChallengeId = SyntheticUserId.ToString("D"),
        privacyConsent = new { noticeVersion = "synthetic-notice", accepted = true, acceptedAtUtc = DateTimeOffset.UnixEpoch },
        profile = new
        {
            name = "Synthetic Registration",
            birthDate = new DateOnly(SyntheticBirthYear, SyntheticBirthMonth, SyntheticBirthDay),
            heightCm = SyntheticHeightCm,
            weightKg = SyntheticWeightKg,
            workoutDaysPerWeek = SyntheticWorkoutDays,
            cycleLengthDays = SyntheticCycleDays,
            selectedActivityTypes = new[] { SyntheticActivity },
            useMetricSystem = true
        },
        quizResponses = new[] { new { questionId = SyntheticQuestionId, answerId = SyntheticAnswerId } }
    };

    private sealed class Fixture(WebApplication app, RegistrationSpy registration, AuthenticationSpy authentication) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public RegistrationSpy Registration { get; } = registration;
        public AuthenticationSpy Authentication { get; } = authentication;

        public static async Task<Fixture> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic",
                ["Authentication:SigningKey"] = "synthetic-registration-test-signing-key-only",
                ["Authentication:Issuer"] = "synthetic-registration-tests",
                ["Authentication:Audience"] = "synthetic-registration-client"
            });
            var registration = new RegistrationSpy();
            var authentication = new AuthenticationSpy();
            var app = ApiApplication.Build(builder, services =>
            {
                services.RemoveAll<ICompleteAccountRegistrationUseCase>();
                services.AddSingleton<ICompleteAccountRegistrationUseCase>(registration);
                services.RemoveAll<IAccountAuthenticationUseCase>();
                services.AddSingleton<IAccountAuthenticationUseCase>(authentication);
            });
            await app.StartAsync();
            return new Fixture(app, registration, authentication);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class RegistrationSpy : ICompleteAccountRegistrationUseCase
    {
        public CompleteAccountRegistrationStatus Status { get; set; } = CompleteAccountRegistrationStatus.Created;
        public string? ErrorMessage { get; set; }
        public CompleteAccountRegistrationRequest? LastRequest { get; private set; }

        public Task<CompleteAccountRegistrationResult> ExecuteAsync(CompleteAccountRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new CompleteAccountRegistrationResult(Status, SyntheticUserId, ErrorMessage));
        }
    }

    private sealed class AuthenticationSpy : IAccountAuthenticationUseCase
    {
        public SessionOperationStatus Status { get; set; } = SessionOperationStatus.Succeeded;
        public PasswordLoginRequest? LastLogin { get; private set; }
        public RefreshSessionRequest? LastRefresh { get; private set; }
        private readonly SessionTokens tokens = new(SyntheticUserId, "synthetic-access", SyntheticRefreshToken, DateTimeOffset.MaxValue);

        public Task<SessionOperationResult> LoginWithPasswordAsync(PasswordLoginRequest request, string? trustedSourceAddress,
            CancellationToken cancellationToken)
        {
            LastLogin = request;
            return Task.FromResult(new SessionOperationResult(Status, Status == SessionOperationStatus.Succeeded ? tokens : null));
        }

        public Task<SessionOperationResult> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken)
        {
            LastRefresh = request;
            return Task.FromResult(new SessionOperationResult(Status, Status == SessionOperationStatus.Succeeded ? tokens : null));
        }

        public Task<(string ChallengeId, string Nonce)> CreateAppleNonceAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionOperationResult> LoginWithAppleAsync(AppleLoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RevokeAsync(RevokeSessionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsAccessTokenRevokedAsync(string tokenId, string sessionId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
