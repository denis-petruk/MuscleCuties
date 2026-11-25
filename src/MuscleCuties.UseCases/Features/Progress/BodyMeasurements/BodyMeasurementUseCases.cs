using MuscleCuties.Core.Domain.Progress;

namespace MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

/// <summary>Records a measurement, refusing a value outside the allowed range.</summary>
public sealed class CreateBodyMeasurementUseCase(IBodyMeasurementSession session, IBodyMeasurementStore store)
{
    /// <summary>Records a new body measurement.</summary>
    public async Task<BodyMeasurementOutputDto> ExecuteAsync(CreateBodyMeasurementInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerId = BodyMeasurementUseCaseMapping.RequireOwner(session);
        BodyMeasurement.RequireCommandId(input.CommandId);
        var replay = await store.TryReplayCreateAsync(ownerId, input, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
            return replay;
        var measurement = BodyMeasurement.Create(input.Id,
            BodyMeasurementCodes.ParseSite(input.Site), BodyMeasurementCodes.ParseSide(input.Side),
            input.LocalDate, input.Value, BodyMeasurementCodes.ParseUnit(input.Unit), session.Today);
        var saved = await store.CreateAsync(ownerId, input.CommandId, measurement, cancellationToken)
            .ConfigureAwait(false);
        return BodyMeasurementUseCaseMapping.ToOutput(saved);
    }
}

/// <summary>Corrects a measurement, keeping the original timestamp order.</summary>
public sealed class CorrectBodyMeasurementUseCase(IBodyMeasurementSession session, IBodyMeasurementStore store)
{
    /// <summary>Corrects an existing body measurement.</summary>
    public async Task<BodyMeasurementOutputDto> ExecuteAsync(CorrectBodyMeasurementInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerId = BodyMeasurementUseCaseMapping.RequireOwner(session);
        BodyMeasurement.RequireCommandId(input.CommandId);
        var replay = await store.TryReplayCorrectionAsync(ownerId, input, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
            return replay;
        var measurement = await store.LoadAsync(ownerId, input.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Measurement was not found for the current account.");
        measurement.Correct(input.ExpectedVersion, BodyMeasurementCodes.ParseSite(input.Site),
            BodyMeasurementCodes.ParseSide(input.Side), input.LocalDate, input.Value,
            BodyMeasurementCodes.ParseUnit(input.Unit), session.Today);
        var saved = await store.CorrectAsync(ownerId, input.CommandId, measurement,
            input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return BodyMeasurementUseCaseMapping.ToOutput(saved);
    }
}

/// <summary>Removes a measurement, refusing one that belongs to another account.</summary>
public sealed class DeleteBodyMeasurementUseCase(IBodyMeasurementSession session, IBodyMeasurementStore store)
{
    /// <summary>Deletes a body measurement.</summary>
    public async Task ExecuteAsync(DeleteBodyMeasurementInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerId = BodyMeasurementUseCaseMapping.RequireOwner(session);
        BodyMeasurement.RequireCommandId(input.CommandId);
        if (await store.WasDeletionAppliedAsync(ownerId, input, cancellationToken).ConfigureAwait(false))
            return;
        var measurement = await store.LoadAsync(ownerId, input.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Measurement was not found for the current account.");
        measurement.Delete(input.ExpectedVersion);
        await store.DeleteAsync(ownerId, input.CommandId, measurement,
            input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Reads the measurement history for a range.</summary>
public sealed class ReadBodyMeasurementHistoryUseCase(IBodyMeasurementSession session, IBodyMeasurementStore store)
{
    /// <summary>The measurement history for the requested range.</summary>
    public async Task<BodyMeasurementHistoryOutputDto> ExecuteAsync(BodyMeasurementHistoryInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerId = BodyMeasurementUseCaseMapping.RequireOwner(session);
        BodyMeasurement.RequireHistoryRange(input.From, input.Through, session.Today);
        var readings = await store.LoadHistoryAsync(ownerId, input.From, input.Through, cancellationToken)
            .ConfigureAwait(false);
        return new BodyMeasurementHistoryOutputDto(readings
            .Select(BodyMeasurementUseCaseMapping.ToOutput)
            .ToArray());
    }
}

/// <summary>Maps between the measurement DTOs and the stored rows.</summary>
internal static class BodyMeasurementUseCaseMapping
{
    /// <summary>The account of the session, refusing when it has none.</summary>
    public static Guid RequireOwner(IBodyMeasurementSession session) =>
        session.CurrentUserId != Guid.Empty
            ? session.CurrentUserId
            : throw new UnauthorizedAccessException("An authenticated account is required.");

    /// <summary>Maps a measurement onto its output shape.</summary>
    public static BodyMeasurementOutputDto ToOutput(BodyMeasurement measurement) => new(
        measurement.Id,
        BodyMeasurementCodes.SiteCode(measurement.Site),
        BodyMeasurementCodes.SideCode(measurement.Side),
        measurement.LocalDate,
        measurement.EnteredValue,
        BodyMeasurementCodes.UnitCode(measurement.EnteredUnit),
        measurement.ValueCentimeters,
        measurement.Version);
}
