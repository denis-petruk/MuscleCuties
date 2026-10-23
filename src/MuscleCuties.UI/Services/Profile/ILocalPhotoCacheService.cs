namespace MuscleCuties.UI.Services.Profile;

public interface ILocalPhotoCacheService
{
    Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task StoreAsync(Guid accountId, Guid photoId, Stream source, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(Guid accountId, Guid photoId, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IProtectedPhotoCacheKeyStore
{
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(byte[] key, CancellationToken cancellationToken = default);
    Task RemoveAsync(CancellationToken cancellationToken = default);
}
