using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public sealed class PhotoVaultUseCase(IPhotoVaultStore store) : IPhotoVaultUseCase
{
    public Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public async Task<PhotoComparisonResult?> CompareAsync(int firstEntryId, int secondEntryId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var first = loaded.Photos.FirstOrDefault(photo => photo.Id == firstEntryId);
        var second = loaded.Photos.FirstOrDefault(photo => photo.Id == secondEntryId);
        return first is not null && second is not null &&
               PhotoComparisonRule.CanCompare(first.Id, first.ImagePath, first.IsImageAvailable,
                   second.Id, second.ImagePath, second.IsImageAvailable)
            ? new PhotoComparisonResult(first, second)
            : null;
    }

    public Task<bool> AddAsync(
        AddPhotoVaultEntryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ImagePath))
            return Task.FromResult(false);
        var normalized = request with
        {
            WeightKg = request.WeightKg is { } weight && double.IsFinite(weight) && weight > 0
                ? weight
                : null
        };
        return store.AddAsync(normalized, cancellationToken);
    }

    public Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken = default)
    {
        return entryId <= 0 ? Task.FromResult(false) : store.DeleteAsync(entryId, cancellationToken);
    }
}
