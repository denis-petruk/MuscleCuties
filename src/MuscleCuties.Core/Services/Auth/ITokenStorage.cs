namespace MuscleCuties.Core.Services.Auth;

public interface ITokenStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    void Remove(string key);
    void RemoveAll();
}