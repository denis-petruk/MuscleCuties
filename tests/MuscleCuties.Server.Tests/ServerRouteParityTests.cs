using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Server;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class ServerRouteParityTests
{
    private const string SyntheticConnectionString =
        "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    private const string SigningKey = "synthetic-server-route-signing-key-for-tests-only";
    private const string Issuer = "SyntheticServer";
    private const string Audience = "SyntheticClient";
    private const string QuizRoute = "/api/v1/accounts/registration/quiz";
    private const string ProfileRoute = "/api/v1/profile";
    private const string SyntheticErrorMarker = "synthetic-internal-error-marker";
    private const int QuizQuestionId = 7;
    private const int QuizAnswerId = 11;
    private const int QuizOrderIndex = 2;
    private const int QuizAnswerOrderIndex = 1;
    private const int DefaultMappedValue = 1;
    private const int DefaultChallengePurpose = 1;
    private const int ExpectedCooldownSeconds = 30;
    private const int ExpectedChallengeExpirationSeconds = 300;
    private const int AccessTokenLifetimeMinutes = 5;

    [Fact]
    public async Task EveryMappedApiRouteHasAnExplicitAuthorizationPolicy()
    {
        await using var fixture = await ServerFixture.StartAsync();
        var anonymousRoutes = new HashSet<string>(StringComparer.Ordinal)
        {
            "/api/v1/accounts/registration/quiz",
            "/api/v1/accounts/registration",
            "/api/v1/auth/apple/nonce",
            "/api/v1/auth/password",
            "/api/v1/auth/apple",
            "/api/v1/auth/refresh",
            "/api/v1/auth/password/reset",
            "/api/v1/auth/email-exists",
            "/api/v1/auth/username-exists",
            "/api/v1/email-challenges/start",
            "/api/v1/email-challenges/resend",
            "/api/v1/email-challenges/verify"
        };

        var routes = fixture.Endpoints.Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(routes);
        foreach (var endpoint in routes)
        {
            var route = endpoint.RoutePattern.RawText!;
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var authorized = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            Assert.True(anonymousRoutes.Contains(route) ? anonymous && !authorized : authorized && !anonymous,
                $"Route {route} has unexpected authorization metadata.");
        }
        Assert.All(anonymousRoutes, route => Assert.Contains(routes, endpoint => endpoint.RoutePattern.RawText == route));
    }

    [Fact]
    public async Task RegistrationQuizIsMappedAnonymouslyAndReturnsCatalog()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await fixture.Client.GetAsync(QuizRoute);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Synthetic question", body, StringComparison.Ordinal);
        Assert.Contains("Synthetic answer", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableQuizCatalogReturnsServiceUnavailableRatherThanNotFound()
    {
        await using var fixture = await ServerFixture.StartAsync(emptyQuiz: true);

        var response = await fixture.Client.GetAsync(QuizRoute);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/session")]
    [InlineData(ProfileRoute)]
    [InlineData("/api/v1/profile/units")]
    [InlineData("/api/v1/profile/history")]
    [InlineData("/api/v1/profile/quiz-responses")]
    [InlineData("/api/v1/nutrition/catalog/search?query=synthetic")]
    [InlineData("/api/v1/workouts/catalog")]
    [InlineData("/api/v1/workouts/plan/exercise-exclusions")]
    [InlineData("/api/v1/cycle/history?from=2026-01-01&through=2026-01-31")]
    [InlineData("/api/v1/dashboard/overview?date=2026-01-01")]
    public async Task PrivateGetRoutesRejectAnonymousCallers(string route)
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await fixture.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthRoutesExistAndUnknownRouteDoesNot()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var live = await fixture.Client.GetAsync("/health/live");
        var ready = await fixture.Client.GetAsync("/health/ready");
        var unknown = await fixture.Client.GetAsync("/api/v1/synthetic-missing");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ProtectedRouteRejectsTokensMissingRequiredIdentityClaims(bool includeTokenId, bool includeSessionId)
    {
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(Guid.NewGuid(), includeTokenId, includeSessionId);

        var response = await fixture.Client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileReadUsesAuthenticatedOwnerAndNeverReturnsAnotherOwner()
    {
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        await using var fixture = await ServerFixture.StartAsync();

        fixture.SetBearer(firstOwner);
        var first = await fixture.Client.GetAsync(ProfileRoute);
        var firstBody = await first.Content.ReadAsStringAsync();

        fixture.SetBearer(secondOwner);
        var second = await fixture.Client.GetAsync(ProfileRoute);
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(firstOwner.ToString("D"), firstBody, StringComparison.Ordinal);
        Assert.DoesNotContain(secondOwner.ToString("D"), firstBody, StringComparison.Ordinal);
        Assert.Contains(secondOwner.ToString("D"), secondBody, StringComparison.Ordinal);
        Assert.DoesNotContain(firstOwner.ToString("D"), secondBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileFailureReturnsSanitizedProblemAndCorrelationId()
    {
        await using var fixture = await ServerFixture.StartAsync(profileThrows: true);
        fixture.SetBearer(Guid.NewGuid());

        var response = await fixture.Client.GetAsync(ProfileRoute);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Request-Id"));
        Assert.DoesNotContain(SyntheticErrorMarker, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailChallengeStartMapsRateLimitAndDeliveryFailure()
    {
        await using var fixture = await ServerFixture.StartAsync();
        var request = new { Email = "synthetic@example.test", Purpose = DefaultChallengePurpose };

        fixture.Challenges.NextStatus = EmailChallengeStatus.RateLimited;
        var limited = await fixture.Client.PostAsJsonAsync("/api/v1/email-challenges/start", request);
        fixture.Challenges.NextStatus = EmailChallengeStatus.DeliveryUnavailable;
        var unavailable = await fixture.Client.PostAsJsonAsync("/api/v1/email-challenges/start", request);
        fixture.Challenges.NextStatus = EmailChallengeStatus.Succeeded;
        var succeeded = await fixture.Client.PostAsJsonAsync("/api/v1/email-challenges/start", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(HttpStatusCode.OK, succeeded.StatusCode);
    }

    [Fact]
    public async Task EmailChallengeVerificationRejectsInvalidCode()
    {
        await using var fixture = await ServerFixture.StartAsync();
        fixture.Challenges.NextStatus = EmailChallengeStatus.InvalidOrExpired;

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/email-challenges/verify", new
        {
            ChallengeId = "synthetic-challenge",
            Email = "synthetic@example.test",
            Code = "invalid",
            Purpose = DefaultChallengePurpose
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileWriteRejectsInvalidVersionWithoutStoreMutation()
    {
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(Guid.NewGuid());

        var response = await fixture.Client.PutAsJsonAsync(ProfileRoute, new { ExpectedVersion = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LegacyWorkoutPreferencesCannotChangeAppliedPreferences()
    {
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(Guid.NewGuid());

        var activities = Enum.GetValues<WorkoutActivityType>()
            .Where(activity => activity != WorkoutActivityType.Other)
            .Select(activity => (int)activity)
            .ToArray();
        var weekdays = Enum.GetValues<DayOfWeek>().Select(day => (int)day).ToArray();
        var durations = Enum.GetValues<WorkoutActivityType>()
            .Where(WorkoutPreferencesRule.RequiresSessionDuration)
            .ToDictionary(activity => (int)activity,
                activity => WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)!.Value);
        var frequencies = Enum.GetValues<WorkoutActivityType>()
            .Where(WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency)
            .ToDictionary(activity => (int)activity,
                activity => ActivityPreferenceDefaults.SessionsPerWeek(activity, weekdays.Length)!.Value);

        var response = await fixture.Client.PutAsJsonAsync("/api/v1/profile/workout-preferences", new
        {
            ExpectedVersion = 1,
            ActivityTypes = activities,
            AvailableWeekdays = weekdays,
            ActivitySessionDurationsMinutes = durations,
            EquipmentLevel = Equipment.FullGym.ToString(),
            RequestedSessionsPerWeek = frequencies,
            ExcludedExerciseIds = new HashSet<int>()
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(fixture.Profile.LastWorkoutPreferences);
    }

    [Fact]
    public async Task ProposalWithoutPreferenceDraftIsRejected()
    {
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(Guid.NewGuid());

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/workouts/plan/strength-proposals", new
        {
            Activity = (int)WorkoutActivityType.GeneralStrength,
            LocalDate = DateOnly.MinValue,
            ActivityHistory = (object?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EmailAndUsernameExistsReturnOk()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var emailRes = await fixture.Client.GetAsync("/api/v1/auth/email-exists?email=synthetic@example.test");
        Assert.Equal(HttpStatusCode.OK, emailRes.StatusCode);
        var emailExists = await emailRes.Content.ReadFromJsonAsync<bool>();
        Assert.False(emailExists);

        var usernameRes = await fixture.Client.GetAsync("/api/v1/auth/username-exists?username=syntheticuser");
        Assert.Equal(HttpStatusCode.OK, usernameRes.StatusCode);
        var userExists = await usernameRes.Content.ReadFromJsonAsync<bool>();
        Assert.False(userExists);
    }

    [Fact]
    public async Task PasswordChangeRequiresAuthorization()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/password/change",
            new { CurrentPassword = "OldPassword123!", NewPassword = "NewPassword123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PasswordChangeUsesBearerOwnerAndReturnsNoContent()
    {
        var owner = Guid.NewGuid();
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(owner);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/password/change",
            new { CurrentPassword = "SyntheticOld123!", NewPassword = "SyntheticNew123!" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(owner, fixture.Authentication.LastPasswordChangeOwner);
    }

    [Fact]
    public async Task NutritionSettingsAliasRequiresAuthorization()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await fixture.Client.PutAsJsonAsync("/api/v1/profile/nutrition-settings",
            new { ExpectedVersion = 1, Goal = "MaintainHealth", WeightGoalPace = "Steady", DietaryTags = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NutritionSettingsAliasWritesForBearerOwnerAndReturnsNextVersion()
    {
        var owner = Guid.NewGuid();
        await using var fixture = await ServerFixture.StartAsync();
        fixture.SetBearer(owner);

        var response = await fixture.Client.PutAsJsonAsync("/api/v1/profile/nutrition-settings",
            new { ExpectedVersion = 1, Goal = "MaintainHealth", WeightGoalPace = "Steady", DietaryTags = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(owner, fixture.Profile.LastNutritionPreferences?.UserId);
        Assert.Equal(1, fixture.Profile.LastNutritionPreferences?.ExpectedVersion);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.Equal(2, body?["version"]);
    }

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly WebApplication app;
        public HttpClient Client { get; }
        public StubEmailChallenges Challenges { get; }
        public StubProfileStore Profile { get; }
        public StubAuthentication Authentication { get; }
        public EndpointDataSource Endpoints => app.Services.GetRequiredService<EndpointDataSource>();

        private ServerFixture(WebApplication app, StubEmailChallenges challenges,
            StubProfileStore profile, StubAuthentication authentication)
        {
            this.app = app;
            Challenges = challenges;
            Profile = profile;
            Authentication = authentication;
            Client = app.GetTestClient();
        }

        public static async Task<ServerFixture> StartAsync(bool emptyQuiz = false, bool profileThrows = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = SyntheticConnectionString,
                ["Authentication:SigningKey"] = SigningKey,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:Audience"] = Audience
            });
            var challenges = new StubEmailChallenges();
            var profile = new StubProfileStore(profileThrows);
            var authentication = new StubAuthentication();
            var app = ApiApplication.Build(builder, services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<ICompleteAccountRegistrationStore>();
                services.AddSingleton<ICompleteAccountRegistrationStore>(new StubRegistrationStore(emptyQuiz));
                services.RemoveAll<IServerProfileDataStore>();
                services.AddSingleton<IServerProfileDataStore>(profile);
                services.RemoveAll<IEmailChallengeUseCase>();
                services.AddSingleton<IEmailChallengeUseCase>(challenges);
                services.RemoveAll<IAccountAuthenticationUseCase>();
                services.AddSingleton<IAccountAuthenticationUseCase>(authentication);
            });
            await app.StartAsync();
            return new ServerFixture(app, challenges, profile, authentication);
        }

        public void SetBearer(Guid owner, bool includeTokenId = true, bool includeSessionId = true)
        {
            var claims = new List<Claim> { new("sub", owner.ToString("D")) };
            if (includeTokenId) claims.Add(new Claim("jti", Guid.NewGuid().ToString("D")));
            if (includeSessionId) claims.Add(new Claim("sid", Guid.NewGuid().ToString("D")));
            var token = new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = Issuer,
                Audience = Audience,
                Expires = DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    SecurityAlgorithms.HmacSha256)
            });
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class StubRegistrationStore(bool emptyQuiz) : ICompleteAccountRegistrationStore
    {
        public Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AccountRegistrationQuizQuestion>>(emptyQuiz
                ? []
                : [new AccountRegistrationQuizQuestion(QuizQuestionId, "Synthetic question", QuizOrderIndex,
                    QuizQuestionType.Goal,
                    [new AccountRegistrationQuizAnswer(QuizAnswerId, "Synthetic answer", QuizAnswerOrderIndex, DefaultMappedValue)])]);

        public Task<RegistrationPrivacyConsent?> GetPendingPrivacyConsentAsync(
            string emailReceiptId, string challengeId, string normalizedEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompleteAccountRegistrationResult> CreateAccountAsync(
            CompleteAccountRegistrationRequest request, string normalizedEmail, string normalizedUsername,
            string passwordHash, string emailReceiptId, DateTimeOffset emailReceiptExpiresAt,
            RegistrationPrivacyConsent privacyConsent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubProfileStore(bool throws) : IServerProfileDataStore
    {
        public UpdateServerNutritionPreferencesRequest? LastNutritionPreferences { get; private set; }
        public UpdateServerWorkoutPreferencesRequest? LastWorkoutPreferences { get; private set; }
        public Task<ServerProfileSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
            throws
                ? throw new InvalidOperationException(SyntheticErrorMarker)
                : Task.FromResult<ServerProfileSnapshot?>(new ServerProfileSnapshot(
                    Name: userId.ToString("D"),
                    DateOfBirth: null,
                    Height: decimal.Zero,
                    Weight: decimal.Zero,
                    Goal: "synthetic",
                    WeightGoalPace: "synthetic",
                    TrainingExperienceLevel: "synthetic",
                    WorkoutDaysPerWeek: 0,
                    AvailableWorkoutWeekdaysJson: "[]",
                    CycleLength: 0,
                    DietaryTags: "{}",
                    ActivitySessionDurationsJson: "{}",
                    EquipmentLevel: "synthetic",
                    PreferredWorkoutActivityTypes: "[]",
                    UnitSystem: "synthetic",
                    BodyWeightUnit: "synthetic",
                    FoodMassUnit: "synthetic",
                    HeightUnit: "synthetic",
                    DistanceUnit: "synthetic",
                    EnergyUnit: "synthetic",
                    NutritionGoalsJson: "{}",
                    Version: 1,
                    Email: "synthetic",
                    ReproductiveContextJson: "{}"));

        public Task<IReadOnlyList<ServerProfileHistoryEntry>> LoadHistoryAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ServerQuizResponse>> LoadQuizResponsesAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ServerProfileWriteResult> UpdateProfileAsync(UpdateServerProfileRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ServerProfileWriteResult> CorrectQuizResponseAsync(CorrectServerQuizResponseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ServerProfileWriteResult> UpdateNutritionPreferencesAsync(UpdateServerNutritionPreferencesRequest request, CancellationToken cancellationToken) =>
            RecordNutritionPreferences(request);
        private Task<ServerProfileWriteResult> RecordNutritionPreferences(UpdateServerNutritionPreferencesRequest request)
        {
            LastNutritionPreferences = request;
            return Task.FromResult(new ServerProfileWriteResult(ServerProfileWriteStatus.Saved, request.ExpectedVersion + 1));
        }
        public Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(UpdateServerWorkoutPreferencesRequest request, CancellationToken cancellationToken)
        {
            LastWorkoutPreferences = request;
            return Task.FromResult(new ServerProfileWriteResult(ServerProfileWriteStatus.Saved, request.ExpectedVersion + 1));
        }
        public Task<ServerProfileWriteResult> UpdateNutritionTargetsAsync(UpdateServerNutritionTargetsRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubEmailChallenges : IEmailChallengeUseCase
    {
        public EmailChallengeStatus NextStatus { get; set; } = EmailChallengeStatus.Succeeded;
        public Task<EmailChallengeOperationResult> StartAsync(StartEmailChallengeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailChallengeOperationResult(NextStatus, "synthetic-challenge",
                ExpectedCooldownSeconds, ExpectedChallengeExpirationSeconds));
        public Task<EmailChallengeOperationResult> ResendAsync(ResendEmailChallengeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailChallengeOperationResult(NextStatus));
        public Task<EmailChallengeOperationResult> VerifyAsync(VerifyEmailChallengeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailChallengeOperationResult(NextStatus));
    }

    private sealed class StubAuthentication : IAccountAuthenticationUseCase
    {
        public Guid? LastPasswordChangeOwner { get; private set; }
        public Task<(string ChallengeId, string Nonce)> CreateAppleNonceAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<SessionOperationResult> LoginWithPasswordAsync(PasswordLoginRequest request, string? trustedSourceAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<SessionOperationResult> LoginWithAppleAsync(AppleLoginRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<SessionOperationResult> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> RevokeAsync(RevokeSessionRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> IsAccessTokenRevokedAsync(string tokenId, string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<PasswordChangeStatus> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken) =>
            RecordPasswordChange(userId);
        private Task<PasswordChangeStatus> RecordPasswordChange(Guid userId)
        {
            LastPasswordChangeOwner = userId;
            return Task.FromResult(PasswordChangeStatus.Succeeded);
        }
    }
}
