using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>One photo vault entry, with the date it was taken.</summary>
public sealed record PhotoVaultEntryDto(
    int Id,
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhaseDto Phase,
    bool IsImageAvailable = false);

/// <summary>The vault as loaded, with the cursor for older photos.</summary>
public sealed record PhotoVaultLoadResult(CyclePhaseDto CurrentPhase, IReadOnlyList<PhotoVaultEntryDto> Photos);

/// <summary>Two photos compared, with the change between them.</summary>
public sealed record PhotoComparisonResult(PhotoVaultEntryDto First, PhotoVaultEntryDto Second);

/// <summary>A photo to add, naming the upload it came from.</summary>
public sealed record AddPhotoVaultEntryRequest(
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhaseDto Phase);

/// <summary>The photo vault as the screen uses it.</summary>
public interface IPhotoVaultUseCase
{
    Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task<PhotoComparisonResult?> CompareAsync(int firstEntryId, int secondEntryId,
        CancellationToken cancellationToken = default);
    Task<bool> AddAsync(AddPhotoVaultEntryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken = default);
}
