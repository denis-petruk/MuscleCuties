using System.Security.Cryptography;

namespace MuscleCuties.App.Services.Security;

public sealed class AppDatabaseKeyProvider : IAppDatabaseKeyProvider
{
    private const string StorageKey = "musclecuties_sqlcipher_key_v1";
    private string? _databasePassword;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_databasePassword))
            return;

        var stored = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(stored))
        {
            stored = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await SecureStorage.Default.SetAsync(StorageKey, stored).ConfigureAwait(false);
        }

        _databasePassword = stored;
    }

    public string GetDatabasePassword()
    {
        if (string.IsNullOrWhiteSpace(_databasePassword))
            throw new InvalidOperationException("The SQLCipher key must be initialized before opening AppDatabase.");

        return _databasePassword;
    }
}
