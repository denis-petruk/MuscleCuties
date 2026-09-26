namespace MuscleCuties.App.Infrastructure.Network;

public sealed class SecureStorageFdcApiKeyProvider : IFdcApiKeyProvider
{
    private const string StorageKey = "fdc_api_key";

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stored = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

#if DEBUG
        var developmentKey = Environment.GetEnvironmentVariable(string.Concat("FDC", "_", "API", "_", "KEY"));
        if (!string.IsNullOrWhiteSpace(developmentKey))
        {
            await SecureStorage.Default.SetAsync(StorageKey, developmentKey).ConfigureAwait(false);
            return developmentKey;
        }
#endif

        return null;
    }
}
