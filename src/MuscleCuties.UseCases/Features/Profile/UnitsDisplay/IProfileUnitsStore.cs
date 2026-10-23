using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

public interface ICurrentProfileUserIdProvider
{
    Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
}

public interface IProfileUnitsStore
{
    Task<DisplayUnits?> LoadCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveCurrentWithSnapshotAsync(
        DisplayUnits settings,
        string snapshotReason,
        CancellationToken cancellationToken = default);
}
