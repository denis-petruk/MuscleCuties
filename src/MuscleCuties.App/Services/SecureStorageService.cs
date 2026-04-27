using MuscleCuties.Core.Services;

namespace MuscleCuties.App.Services;

public class SecureStorageService : ISecureStorage
{
    public Task<string?> GetAsync(string key) =>
        SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value) =>
        SecureStorage.Default.SetAsync(key, value);

    public void Remove(string key) =>
        SecureStorage.Default.Remove(key);

    public void RemoveAll() =>
        SecureStorage.Default.RemoveAll();
}
