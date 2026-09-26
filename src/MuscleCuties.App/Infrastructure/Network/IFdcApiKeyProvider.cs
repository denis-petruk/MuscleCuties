namespace MuscleCuties.App.Infrastructure.Network;

public interface IFdcApiKeyProvider
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken);
}
