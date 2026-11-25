namespace MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

/// <summary>The units as the server stores them.</summary>
public sealed record ServerProfileUnitsSnapshot(
    bool UseMetricSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit,
    int Version);

/// <summary>A units change sent to the server.</summary>
public sealed record SaveServerProfileUnitsRequest(
    Guid UserId,
    int ExpectedVersion,
    bool UseMetricSystem,
    string? BodyWeightUnit,
    string? FoodMassUnit,
    string? HeightUnit,
    string? DistanceUnit,
    string? EnergyUnit);

/// <summary>How the server's units write ended.</summary>
public enum SaveServerProfileUnitsStatus
{
    Saved,
    ProfileMissing,
    VersionConflict,
    InvalidRequest
}

/// <summary>The outcome of that write.</summary>
public sealed record SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus Status, int? Version);

/// <summary>The server units persistence.</summary>
public interface IServerProfileUnitsStore
{
    Task<ServerProfileUnitsSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken);

    Task<SaveServerProfileUnitsResult> SaveAsync(
        SaveServerProfileUnitsRequest request,
        CancellationToken cancellationToken);
}
