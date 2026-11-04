using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.Overview;

public sealed class ProfileOverviewUseCase(
    IProfileOverviewStore store,
    IProfilePurgePort purgePort) : IProfileOverviewUseCase
{
    public async Task<ProfileOverviewDto> LoadAsync(CancellationToken cancellationToken = default)
    {
        var source = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var phase = source.PredictedPhase != CyclePhase.Unknown
            ? source.PredictedPhase
            : source.StoredPhase is { } stored && stored != CyclePhase.Unknown
                ? stored
                : CyclePhase.Unknown;
        return source.Overview with { CurrentPhase = CycleContractMapper.ToDto(phase) };
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return store.LogoutAsync(cancellationToken);
    }

    public Task<string?> UpdateProfileImageAsync(
        string? imagePath, CancellationToken cancellationToken = default)
    {
        return store.UpdateProfileImageAsync(imagePath, cancellationToken);
    }

    public Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        return purgePort.PurgeCurrentAccountAsync(cancellationToken);
    }
}
