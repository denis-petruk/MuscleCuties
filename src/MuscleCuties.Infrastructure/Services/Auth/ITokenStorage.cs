namespace MuscleCuties.Core.Services.Auth;

/// <summary>Where the session tokens are kept, so the client never holds credentials itself.</summary>
public interface ITokenStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    void Remove(string key);
    void RemoveAll();
}
