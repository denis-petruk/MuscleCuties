using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

/// <summary>Applies a units change on the server.</summary>
public sealed class ServerProfileUnitsUseCase(IServerProfileUnitsStore store)
{
    /// <summary>The server's stored units, or null when the profile has none yet.</summary>
    public Task<ServerProfileUnitsSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return userId == Guid.Empty
            ? Task.FromResult<ServerProfileUnitsSnapshot?>(null)
            : store.LoadAsync(userId, cancellationToken);
    }

    /// <summary>Saves the user's unit preference.</summary>
    public Task<SaveServerProfileUnitsResult> SaveAsync(
        SaveServerProfileUnitsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty || request.ExpectedVersion <= 0)
            return Task.FromResult(new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.InvalidRequest, null));

        var units = DisplayUnits.FromInput(
            request.UseMetricSystem,
            request.BodyWeightUnit,
            request.FoodMassUnit,
            request.HeightUnit,
            request.DistanceUnit,
            request.EnergyUnit);
        if (!units.IsSupported)
            return Task.FromResult(new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.InvalidRequest, null));

        return store.SaveAsync(request, cancellationToken);
    }
}
