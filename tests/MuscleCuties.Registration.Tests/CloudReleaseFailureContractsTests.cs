using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;

namespace MuscleCuties.Registration.Tests;

public sealed class CloudReleaseFailureContractsTests
{
    [Xunit.Fact]
    public async Task ProfileImageUpdateFailsClosedBeforeBackendOrFileMutation()
    {
        var api = new CountingApiClient();
        var store = new CloudProfileOverviewStore(api, null!);
        var path = Path.Combine(Path.GetTempPath(), $"synthetic-profile-{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3]);

            var uploadError = await Xunit.Assert.ThrowsAsync<NotSupportedException>(() =>
                store.UpdateProfileImageAsync(path, CancellationToken.None));
            var clearError = await Xunit.Assert.ThrowsAsync<NotSupportedException>(() =>
                store.UpdateProfileImageAsync(null, CancellationToken.None));

            Xunit.Assert.Contains("validated, account-scoped first-party upload contract", uploadError.Message);
            Xunit.Assert.Equal(uploadError.Message, clearError.Message);
            Xunit.Assert.True(File.Exists(path));
            Xunit.Assert.Equal(0, api.CallCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Xunit.Fact]
    public async Task PersonalInfoLoadDoesNotTreatServerFailureAsAnEmptyProfile()
    {
        var store = new CloudProfilePersonalInfoStore(new UnavailableApiClient());

        var error = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.LoadAsync(CancellationToken.None));

        Xunit.Assert.Contains("ServerError", error.Message);
    }

    [Xunit.Fact]
    public async Task UnitsLoadDoesNotTreatServerFailureAsMissingSettings()
    {
        var store = new CloudProfileUnitsStore(new UnavailableApiClient());

        var error = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.LoadCurrentAsync(CancellationToken.None));

        Xunit.Assert.Contains("ServerError", error.Message);
    }

    private sealed class UnavailableApiClient : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.ServerError, default));

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingApiClient : IMuscleCutiesBackendApiClient
    {
        public int CallCount { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, default));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
        }
    }
}
