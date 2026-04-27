namespace MuscleCuties.Core.Services;

public interface ISecureStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    void Remove(string key);
    void RemoveAll();
}
