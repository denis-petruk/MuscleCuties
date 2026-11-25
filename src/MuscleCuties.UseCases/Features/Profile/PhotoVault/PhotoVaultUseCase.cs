using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>Loads and adds photos, publishing an upload only after its bytes are verified.</summary>
public sealed class PhotoVaultUseCase(IPhotoVaultStore store) : IPhotoVaultUseCase
{
    /// <summary>Her photos newest first, with the cursor for older ones.</summary>
    public Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    /// <summary>Compares two saved photos and returns the change summary.</summary>
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

    /// <summary>Adds a photo to the vault after validating its bytes.</summary>
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

    /// <summary>Removes the photo and its stored object, refusing one that is not hers.</summary>
    public Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken = default)
    {
        return entryId <= 0 ? Task.FromResult(false) : store.DeleteAsync(entryId, cancellationToken);
    }
}
