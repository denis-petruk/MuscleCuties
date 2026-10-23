namespace MuscleCuties.UI.Infrastructure.Network;

public interface IFdcApiKeyProvider
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken);
}
