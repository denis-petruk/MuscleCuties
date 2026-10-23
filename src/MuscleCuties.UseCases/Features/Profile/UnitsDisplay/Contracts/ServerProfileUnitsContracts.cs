namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

public sealed record ServerProfileUnitsSnapshot(
    bool UseMetricSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit,
    int Version);

public sealed record SaveServerProfileUnitsRequest(
    Guid UserId,
    int ExpectedVersion,
    bool UseMetricSystem,
    string? BodyWeightUnit,
    string? FoodMassUnit,
    string? HeightUnit,
    string? DistanceUnit,
    string? EnergyUnit);

public enum SaveServerProfileUnitsStatus
{
    Saved,
    ProfileMissing,
    VersionConflict,
    InvalidRequest
}

public sealed record SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus Status, int? Version);

public interface IServerProfileUnitsStore
{
    Task<ServerProfileUnitsSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken);

    Task<SaveServerProfileUnitsResult> SaveAsync(
        SaveServerProfileUnitsRequest request,
        CancellationToken cancellationToken);
}
