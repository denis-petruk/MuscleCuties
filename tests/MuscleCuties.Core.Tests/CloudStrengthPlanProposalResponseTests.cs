using System.Net;
using System.Text;
using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CloudStrengthPlanProposalResponseTests
{
    private const string ApiBaseAddress = "https://example.test";
    private const string PolicyName = "synthetic-policy";
    private const string PolicyVersion = "synthetic-version";
    private const string RulesetVersion = "synthetic-ruleset";
    private const string InvalidInputStatus = "InvalidInput";
    private const string TimingLimitation = "StrengthWarmupOrCooldownDurationMissing";
    private static readonly DateOnly SampleDate = new(2026, 10, 25);

    [Fact]
    public async Task ValidationResponseWithPreviewPreservesPlanningLimitation()
    {
        var preview = new StrengthPlanPreviewResponseV020(
            PolicyName, PolicyVersion, RulesetVersion, InvalidInputStatus,
            0, [], [TimingLimitation], [], []);
        var responseBody = JsonSerializer.Serialize(
            new StrengthPlanProposalCreatedV020(Guid.Empty, preview, false),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var httpClient = new HttpClient(new StaticResponseHandler(responseBody))
        {
            BaseAddress = new Uri(ApiBaseAddress)
        };
        var apiClient = new MuscleCutiesBackendApiClient(
            httpClient, new EmptyTokenStore(), null!, new OnlineAuthorityGate());
        var store = new CloudStrengthPlanProposalStoreV020(apiClient);

        var result = await store.CreateAsync(new StrengthPlanPreviewRequestV020(
            WorkoutActivityDto.GeneralStrength, SampleDate, null), CancellationToken.None);

        Assert.False(result.IsPersisted);
        Assert.Equal(InvalidInputStatus, result.Preview.Status);
        Assert.Contains(TimingLimitation, result.Preview.LimitationCodes);
    }

    [Fact]
    public async Task ApplyServerFailureIsReportedAsConnectionFailure()
    {
        using var httpClient = new HttpClient(new ServerFailureHandler())
        {
            BaseAddress = new Uri(ApiBaseAddress)
        };
        var apiClient = new MuscleCutiesBackendApiClient(
            httpClient, new EmptyTokenStore(), null!, new OnlineAuthorityGate());
        var store = new CloudStrengthPlanProposalStoreV020(apiClient);
        var request = new AcceptStrengthPlanProposalRequestV020(Guid.NewGuid());

        await Assert.ThrowsAsync<HttpRequestException>(() => store.AcceptAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task OfflineApplyIsReportedAsConnectionFailure()
    {
        using var httpClient = new HttpClient(new ServerFailureHandler())
        {
            BaseAddress = new Uri(ApiBaseAddress)
        };
        var apiClient = new MuscleCutiesBackendApiClient(
            httpClient, new EmptyTokenStore(), null!, new OfflineAuthorityGate());
        var store = new CloudStrengthPlanProposalStoreV020(apiClient);
        var request = new AcceptStrengthPlanProposalRequestV020(Guid.NewGuid());

        await Assert.ThrowsAsync<HttpRequestException>(() => store.AcceptAsync(request, CancellationToken.None));
    }

    private sealed class ServerFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private sealed class StaticResponseHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }

    private sealed class EmptyTokenStore : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendSessionTokens?>(null);

        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearTokensAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OnlineAuthorityGate : ICloudAuthorityGate
    {
        public int AuthorityVersion => 1;
        public CloudAuthorityMode CurrentMode => CloudAuthorityMode.ServerAuthoritativeCutover;
        public bool IsNetworkOnline => true;
        public void SetAuthorityMode(CloudAuthorityMode mode) { }
        public void SetNetworkOnline(bool isOnline) { }
        public OfflineWriteCheckResult EvaluateWriteAllowed(string operationName) => new(true, null, null);
    }

    private sealed class OfflineAuthorityGate : ICloudAuthorityGate
    {
        public int AuthorityVersion => 1;
        public CloudAuthorityMode CurrentMode => CloudAuthorityMode.ServerAuthoritativeCutover;
        public bool IsNetworkOnline => false;
        public void SetAuthorityMode(CloudAuthorityMode mode) { }
        public void SetNetworkOnline(bool isOnline) { }
        public OfflineWriteCheckResult EvaluateWriteAllowed(string operationName) => new(false, null, null);
    }
}
