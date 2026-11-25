using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

/// <summary>Who the current request belongs to, which every scoped write needs.</summary>
public interface ICurrentProfileUserIdProvider
{
    Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>The units persistence.</summary>
public interface IProfileUnitsStore
{
    Task<DisplayUnits?> LoadCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveCurrentWithSnapshotAsync(
        DisplayUnits settings,
        string snapshotReason,
        CancellationToken cancellationToken = default);
}
