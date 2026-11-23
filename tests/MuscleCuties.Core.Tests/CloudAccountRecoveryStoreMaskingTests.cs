using System.Net;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CloudAccountRecoveryStoreMaskingTests
{
    private const string Address = "denis.petruk@example.test";

    [Theory]
    [InlineData("start")]
    [InlineData("resend")]
    public async Task RecoveryShowsAMaskedAddressAndKeepsTheFullOneForTheFlow(string entryPoint)
    {
        var api = new ChallengeApiStub();
        var store = new CloudAccountRecoveryStore(
            api, new CloudEmailChallengeGateway(new PendingRegistrationService(), api));

        var result = entryPoint == "start"
            ? await store.StartAsync(Address, CancellationToken.None)
            : await store.ResendAsync(Guid.NewGuid().ToString("D"), Address, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(Address, result.Email);
        Assert.Equal("d***@example.test", result.MaskedEmail);
        Assert.DoesNotContain("denis.petruk", result.MaskedEmail, StringComparison.Ordinal);
    }

    private sealed class ChallengeApiStub : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, default));

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) != typeof(CloudEmailChallengeStart))
                throw new NotSupportedException(typeof(TResponse).Name);

            var start = new CloudEmailChallengeStart(Guid.NewGuid().ToString("D"), 60, 600);
            return Task.FromResult(new BackendApiResponse<TResponse>(
                BackendApiStatus.Success, (TResponse)(object)start, ResponseStatusCode: HttpStatusCode.OK));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
    }
}
