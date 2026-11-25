using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.Overview;

/// <summary>Builds the profile summary from one read.</summary>
public sealed class ProfileOverviewUseCase(
    IProfileOverviewStore store,
    IProfilePurgePort purgePort) : IProfileOverviewUseCase
{
    /// <summary>The profile summary, read from one consistent snapshot.</summary>
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

    /// <summary>Clears the session and the cached snapshots, so the next sign-in starts clean.</summary>
    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return store.LogoutAsync(cancellationToken);
    }

    /// <summary>Stores the profile image and returns its path.</summary>
    public Task<string?> UpdateProfileImageAsync(
        string? imagePath, CancellationToken cancellationToken = default)
    {
        return store.UpdateProfileImageAsync(imagePath, cancellationToken);
    }

    /// <summary>Deletes the account's data locally and on the server, clearing the session only after both succeed.</summary>
    public Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        return purgePort.PurgeCurrentAccountAsync(cancellationToken);
    }
}
