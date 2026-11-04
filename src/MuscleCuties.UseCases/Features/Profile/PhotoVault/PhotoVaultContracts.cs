using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public sealed record PhotoVaultEntryDto(
    int Id,
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhaseDto Phase,
    bool IsImageAvailable = false);

public sealed record PhotoVaultLoadResult(CyclePhaseDto CurrentPhase, IReadOnlyList<PhotoVaultEntryDto> Photos);

public sealed record PhotoComparisonResult(PhotoVaultEntryDto First, PhotoVaultEntryDto Second);

public sealed record AddPhotoVaultEntryRequest(
    string ImagePath,
    DateTime Date,
    double? WeightKg,
    string Notes,
    CyclePhaseDto Phase);

public interface IPhotoVaultUseCase
{
    Task<PhotoVaultLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task<PhotoComparisonResult?> CompareAsync(int firstEntryId, int secondEntryId,
        CancellationToken cancellationToken = default);
    Task<bool> AddAsync(AddPhotoVaultEntryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int entryId, CancellationToken cancellationToken = default);
}
