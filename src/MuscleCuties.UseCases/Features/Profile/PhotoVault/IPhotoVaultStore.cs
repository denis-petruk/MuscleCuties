namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>The photo vault persistence.</summary>
public interface IPhotoVaultStore
{
    Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task<bool> AddAsync(AddPhotoVaultEntryRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken);
}
