using MuscleCuties.Core.Domain.Progress;

namespace MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

public sealed record CreateBodyMeasurementInputDto(
    Guid Id, Guid CommandId, string Site, string Side, DateOnly LocalDate, double Value, string Unit);

public sealed record CorrectBodyMeasurementInputDto(
    Guid Id, Guid CommandId, int ExpectedVersion, string Site, string Side,
    DateOnly LocalDate, double Value, string Unit);

public sealed record DeleteBodyMeasurementInputDto(Guid Id, Guid CommandId, int ExpectedVersion);

public sealed record BodyMeasurementHistoryInputDto(DateOnly From, DateOnly Through);

public sealed record BodyMeasurementOutputDto(
    Guid Id, string Site, string Side, DateOnly LocalDate, decimal EnteredValue,
    string EnteredUnit, decimal ValueCentimeters, int Version);

public sealed record BodyMeasurementHistoryOutputDto(IReadOnlyList<BodyMeasurementOutputDto> Readings);

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
