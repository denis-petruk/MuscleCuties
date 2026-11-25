using MuscleCuties.Core.Domain.Progress;

namespace MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

/// <summary>A measurement to record, with the units it is given in.</summary>
public sealed record CreateBodyMeasurementInputDto(
    Guid Id, Guid CommandId, string Site, string Side, DateOnly LocalDate, double Value, string Unit);

/// <summary>A correction of a recorded measurement.</summary>
public sealed record CorrectBodyMeasurementInputDto(
    Guid Id, Guid CommandId, int ExpectedVersion, string Site, string Side,
    DateOnly LocalDate, double Value, string Unit);

/// <summary>A measurement to remove.</summary>
public sealed record DeleteBodyMeasurementInputDto(Guid Id, Guid CommandId, int ExpectedVersion);

/// <summary>The range the history is read for.</summary>
public sealed record BodyMeasurementHistoryInputDto(DateOnly From, DateOnly Through);

/// <summary>One measurement as the history shows it.</summary>
public sealed record BodyMeasurementOutputDto(
    Guid Id, string Site, string Side, DateOnly LocalDate, decimal EnteredValue,
    string EnteredUnit, decimal ValueCentimeters, int Version);

/// <summary>The measurement history for the range.</summary>
public sealed record BodyMeasurementHistoryOutputDto(IReadOnlyList<BodyMeasurementOutputDto> Readings);

/// <summary>The measurement persistence, scoped to the signed-in account.</summary>
public interface IBodyMeasurementSession
{
    Guid CurrentUserId { get; }
    DateOnly Today { get; }
}

/// <summary>
/// Owner-filtered adapter contract. Implementations must commit command identity and mutation atomically,
/// enforce the owner/site/side/date uniqueness rule and compare expected versions at write time.
/// </summary>
public interface IBodyMeasurementStore
{
    // Return an acknowledged prior result for the same owner, command ID and exact payload.
    // A reused command ID with a different payload must fail; the mutation methods must also
    // enforce this atomically when concurrent retries race the receipt lookup.
    Task<BodyMeasurementOutputDto?> TryReplayCreateAsync(Guid ownerId,
        CreateBodyMeasurementInputDto input, CancellationToken cancellationToken);

    Task<BodyMeasurementOutputDto?> TryReplayCorrectionAsync(Guid ownerId,
        CorrectBodyMeasurementInputDto input, CancellationToken cancellationToken);

    Task<bool> WasDeletionAppliedAsync(Guid ownerId,
        DeleteBodyMeasurementInputDto input, CancellationToken cancellationToken);

    Task<BodyMeasurement> CreateAsync(Guid ownerId, Guid commandId, BodyMeasurement measurement,
        CancellationToken cancellationToken);

    Task<BodyMeasurement?> LoadAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    Task<BodyMeasurement> CorrectAsync(Guid ownerId, Guid commandId, BodyMeasurement measurement,
        int expectedVersion, CancellationToken cancellationToken);

    Task DeleteAsync(Guid ownerId, Guid commandId, BodyMeasurement measurement,
        int expectedVersion, CancellationToken cancellationToken);

    // Returns only active readings, deterministically ordered by date/site/side/ID.
    Task<IReadOnlyList<BodyMeasurement>> LoadHistoryAsync(Guid ownerId, DateOnly from,
        DateOnly through, CancellationToken cancellationToken);
}
