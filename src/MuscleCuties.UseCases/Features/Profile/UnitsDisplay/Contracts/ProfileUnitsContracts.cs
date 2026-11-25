namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

/// <summary>Why a units change was refused.</summary>
public enum ProfileUnitsError
{
    None,
    ProfileMissing
}

/// <summary>The units in force.</summary>
public sealed record ProfileUnitsResponse(
    bool UseMetricSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit);

/// <summary>The units she is choosing.</summary>
public sealed record SaveProfileUnitsRequest(
    bool UseMetricSystem,
    string? BodyWeightUnit,
    string? FoodMassUnit,
    string? HeightUnit,
    string? DistanceUnit,
    string? EnergyUnit);

/// <summary>The stored units with the version a save must send back.</summary>
public sealed record LoadProfileUnitsResult(ProfileUnitsResponse? Settings, ProfileUnitsError Error);

/// <summary>Whether the units were saved.</summary>
public sealed record SaveProfileUnitsResult(ProfileUnitsError Error)
{
    /// <summary>True when the units were saved without an error.</summary>
    public bool Succeeded => Error == ProfileUnitsError.None;
}

/// <summary>The units setting.</summary>
public interface IProfileUnitsUseCase
{
    Task<LoadProfileUnitsResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<SaveProfileUnitsResult> SaveAsync(
        SaveProfileUnitsRequest request,
        CancellationToken cancellationToken = default);
}
