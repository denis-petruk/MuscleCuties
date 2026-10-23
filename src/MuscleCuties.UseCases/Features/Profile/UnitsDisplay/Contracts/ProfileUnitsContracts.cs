namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

public enum ProfileUnitsError
{
    None,
    ProfileMissing
}

public sealed record ProfileUnitsResponse(
    bool UseMetricSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit);

public sealed record SaveProfileUnitsRequest(
    bool UseMetricSystem,
    string? BodyWeightUnit,
    string? FoodMassUnit,
    string? HeightUnit,
    string? DistanceUnit,
    string? EnergyUnit);

public sealed record LoadProfileUnitsResult(ProfileUnitsResponse? Settings, ProfileUnitsError Error);

public sealed record SaveProfileUnitsResult(ProfileUnitsError Error)
{
    public bool Succeeded => Error == ProfileUnitsError.None;
}

public interface IProfileUnitsUseCase
{
    Task<LoadProfileUnitsResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<SaveProfileUnitsResult> SaveAsync(
        SaveProfileUnitsRequest request,
        CancellationToken cancellationToken = default);
}
