using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

public sealed class ServerProfileUnitsUseCase(IServerProfileUnitsStore store)
{
    public Task<ServerProfileUnitsSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return userId == Guid.Empty
            ? Task.FromResult<ServerProfileUnitsSnapshot?>(null)
            : store.LoadAsync(userId, cancellationToken);
    }

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
