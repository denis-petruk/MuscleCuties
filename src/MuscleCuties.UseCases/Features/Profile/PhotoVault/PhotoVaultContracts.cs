using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public sealed record PhotoVaultEntryDto(
    int Id,
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhase Phase,
    bool IsImageAvailable = false);

public sealed record PhotoVaultLoadResult(CyclePhase CurrentPhase, IReadOnlyList<PhotoVaultEntryDto> Photos);

public sealed record PhotoComparisonResult(PhotoVaultEntryDto First, PhotoVaultEntryDto Second);

public sealed record AddPhotoVaultEntryRequest(
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhase Phase);

public interface IPhotoVaultUseCase
{
    Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task<PhotoComparisonResult?> CompareAsync(int firstEntryId, int secondEntryId,
        CancellationToken cancellationToken = default);
    Task<bool> AddAsync(AddPhotoVaultEntryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken = default);
}
