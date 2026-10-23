using System.Net;
using System.Net.Http.Json;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Caching;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class RegistrationErrorPresentationTests
{
    private const string SyntheticEmail = "registration-error@example.test";
    private const string SyntheticUsername = "registration_error";
    private const string SyntheticPassword = "SyntheticRegistration123!";
    private const string SyntheticPrivacyVersion = "synthetic-notice";
    private const string SyntheticReceipt = "synthetic-receipt";
    private const string RegistrationRoute = "/api/v1/accounts/registration";
    private const string ChallengeStartRoute = "/api/v1/email-challenges/start";
    private const int SyntheticQuestionId = 1;
    private const int SyntheticAnswerId = 1;
    private const int SyntheticWorkoutDays = 3;
    private const int SyntheticCycleDays = 28;
    private const int SyntheticBirthYear = 1996;
    private const int SyntheticBirthMonth = 4;
    private const int SyntheticBirthDay = 12;
    private const float SyntheticHeightCm = 170f;
    private const float SyntheticWeightKg = 70f;

    [Fact]
    public async Task ApiClientReadsServerCodeFieldForValidationErrors()
    {
        using var httpClient = new HttpClient(new RegistrationErrorHandler())
        {
            BaseAddress = new Uri("https://registration.test")
        };
        var client = new MuscleCutiesBackendApiClient(
            httpClient, new EmptyTokenStore(), null!, new VersionedCloudAuthorityGate());

        var response = await client.GetAsync<object>(RegistrationRoute);

        Assert.Equal(BackendApiStatus.ValidationFailed, response.Status);
        Assert.Equal("InvalidQuiz", response.ErrorCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests,
        "Too many code requests in the past 24 hours. Please try again later.")]
    [InlineData(HttpStatusCode.ServiceUnavailable,
        "Verification email is unavailable right now. Please try again later.")]
    [InlineData(HttpStatusCode.InternalServerError,
        "We couldn't send a verification code. Wait a moment, then tap Create account again.")]
    public async Task ChallengeStartFailureShowsRetryMessageAndAllowsRetry(
        HttpStatusCode failureStatus, string expectedMessage)
    {
        using var httpClient = new HttpClient(new ChallengeStartHandler(failureStatus))
        {
            BaseAddress = new Uri("https://registration.test")
        };
        var apiClient = new MuscleCutiesBackendApiClient(
            httpClient, new EmptyTokenStore(), null!, new VersionedCloudAuthorityGate());
        var pending = new PendingRegistrationService();
        var registration = new AccountRegistrationUseCase(new CloudAccountRegistrationGateway(pending, apiClient));
        var request = new StartAccountRegistrationRequest(
            SyntheticEmail, SyntheticUsername, SyntheticPassword, SyntheticPassword,
            new RegistrationPrivacyConsent(PrivacyNotice.CurrentVersion, true, DateTimeOffset.UtcNow));

        var failed = await registration.StartAsync(request);

        Assert.Equal(AccountRegistrationStatus.ChallengeFailed, failed.Status);
        Assert.Equal(expectedMessage, failed.ErrorMessage);
        Assert.Null(pending.CurrentRegistration);

        var retried = await registration.StartAsync(request);

        Assert.Equal(AccountRegistrationStatus.Started, retried.Status);
        Assert.NotNull(retried.AttemptId);
        Assert.NotNull(pending.CurrentRegistration);
    }

    [Theory]
    [InlineData("InvalidQuiz", "We couldn't save your preferences. Please try again later.")]
    [InlineData("InvalidProfile", "Check your profile details, then try again.")]
    [InlineData("validationFailed", "We couldn't finish registration. Please try again.")]
    public async Task QuizCommitShowsActionableMessageInsteadOfServerCode(string serverCode, string expectedMessage)
    {
        var pending = new PendingRegistrationService();
        var challengeId = Guid.CreateVersion7().ToString("D");
        pending.StartEmailRegistration(SyntheticEmail, SyntheticUsername, SyntheticPassword, challengeId,
            SyntheticPrivacyVersion, DateTimeOffset.UtcNow);
        pending.ConfirmChallenge(challengeId, SyntheticReceipt);
        pending.StageProfile(new StagedProfileData(
            "Synthetic Registration", new DateTime(SyntheticBirthYear, SyntheticBirthMonth, SyntheticBirthDay),
            SyntheticHeightCm, SyntheticWeightKg,
            string.Empty, SyntheticWorkoutDays, SyntheticCycleDays,
            new HashSet<WorkoutActivityType> { WorkoutActivityType.StrengthHighIntensity }));
        var store = new CloudOnboardingQuizStore(pending, null!, new RegistrationErrorApiClient(serverCode));

        var result = await store.CommitAsync([new QuizSelectionDto(SyntheticQuestionId, SyntheticAnswerId)],
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.True(result.AccountCreatedNeedsLogin);
    }

    private sealed class RegistrationErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new { code = "InvalidQuiz" })
            });
    }

    private sealed class ChallengeStartHandler(HttpStatusCode failureStatus) : HttpMessageHandler
    {
        private int _startRequests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(false)
                });

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(ChallengeStartRoute, request.RequestUri?.AbsolutePath);
            if (Interlocked.Increment(ref _startRequests) == 1)
                return Task.FromResult(new HttpResponseMessage(failureStatus));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ChallengeId = Guid.NewGuid().ToString("D") })
            });
        }
    }

    private sealed class EmptyTokenStore : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendSessionTokens?>(null);

        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearTokensAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RegistrationErrorApiClient(string code) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.ValidationFailed, default, code));

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
