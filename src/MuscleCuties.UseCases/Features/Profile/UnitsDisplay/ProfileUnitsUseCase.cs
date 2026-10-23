using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

public sealed class ProfileUnitsUseCase(IProfileUnitsStore store) : IProfileUnitsUseCase
{
    private const string SnapshotReason = "UnitsDisplayUpdate";

    public async Task<LoadProfileUnitsResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await store.LoadCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
            return new LoadProfileUnitsResult(null, ProfileUnitsError.ProfileMissing);

        return new LoadProfileUnitsResult(new ProfileUnitsResponse(
            settings.UseMetricSystem,
            settings.BodyWeightUnit,
            settings.FoodMassUnit,
            settings.HeightUnit,
            settings.DistanceUnit,
            settings.EnergyUnit), ProfileUnitsError.None);
    }

    public async Task<SaveProfileUnitsResult> SaveAsync(
        SaveProfileUnitsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = DisplayUnits.FromInput(
            request.UseMetricSystem,
            request.BodyWeightUnit,
            request.FoodMassUnit,
            request.HeightUnit,
            request.DistanceUnit,
            request.EnergyUnit);

        var saved = await store.SaveCurrentWithSnapshotAsync(
            settings, SnapshotReason, cancellationToken).ConfigureAwait(false);
        return new SaveProfileUnitsResult(saved ? ProfileUnitsError.None : ProfileUnitsError.ProfileMissing);
    }
}
