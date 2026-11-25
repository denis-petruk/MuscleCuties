using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>Today's intake as the hydration screen shows it.</summary>
public sealed class GetWaterIntakeUseCase(IWaterIntakeStore store)
{
    /// <summary>The day's intake with its total, answered as an empty day when nothing is logged.</summary>
    public async Task<WaterIntakeDayOutputDto> ExecuteAsync(GetWaterIntakeInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterIntakeOperation.Map(await WaterIntakeOperation.ReadAsync(store, input.AccountId, input.Date, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Adds or removes water, refusing a change that would go below zero.</summary>
public sealed class ChangeWaterIntakeUseCase(IWaterIntakeStore store)
{
    /// <summary>Adds or removes water and returns the new day, refusing a change that would go below zero.</summary>
    public async Task<WaterIntakeDayOutputDto> ExecuteAsync(ChangeWaterIntakeInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stored = await WaterIntakeOperation.ReadAsync(store, input.AccountId, input.Date, cancellationToken).ConfigureAwait(false);
        var kind = input.Kind switch
        {
            WaterIntakeChangeKindDto.Add => WaterIntakeChangeKind.Add,
            WaterIntakeChangeKindDto.Correct => WaterIntakeChangeKind.Correct,
            WaterIntakeChangeKindDto.Undo => WaterIntakeChangeKind.Undo,
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };
        var updated = stored.Day.Apply(new(input.OperationId, input.EntryId, kind, input.Millilitres));
        // A matching durable receipt acknowledges a retry even if the original response was lost.
        if (ReferenceEquals(updated, stored.Day)) return WaterIntakeOperation.Map(stored);
        if (!string.Equals(stored.Version, input.ExpectedVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Intake changed. Reload before editing.");
        cancellationToken.ThrowIfCancellationRequested();
        var committed = await store.TryReplaceAsync(updated, stored.Version, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Intake changed during persistence. Reload before retrying.");
        WaterIntakeOperation.RequireScope(committed, input.AccountId, input.Date);
        return WaterIntakeOperation.Map(committed);
    }
}

/// <summary>Builds the stored operation for an intake change, which is what keeps the day replayable.</summary>
internal static class WaterIntakeOperation
{
    /// <summary>Reads the stored day, creating the empty shape when the day has no rows yet.</summary>
    internal static async Task<StoredWaterIntakeDay> ReadAsync(IWaterIntakeStore store, Guid accountId, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        var empty = WaterIntakeDay.Restore(accountId, date, 0, [], []);
        var stored = await store.ReadAsync(empty.AccountId, date, cancellationToken).ConfigureAwait(false);
        RequireScope(stored, accountId, date);
        return stored;
    }

    /// <summary>Refuses a stored day that belongs to another account, which would otherwise leak into this one.</summary>
    internal static void RequireScope(StoredWaterIntakeDay stored, Guid accountId, DateOnly date)
    {
        if (stored.Day.AccountId != accountId || stored.Day.Date != date)
            throw new InvalidOperationException("Intake storage returned a different account or date.");
    }

    /// <summary>Maps a stored water day onto its output shape.</summary>
    internal static WaterIntakeDayOutputDto Map(StoredWaterIntakeDay stored) => new(stored.Day.AccountId,
        stored.Day.Date, stored.Day.LegacyMillilitres, stored.Day.TotalMillilitres,
        stored.Day.Entries.Select(e => new WaterIntakeEntryOutputDto(e.Id, e.Millilitres, e.Undone)).ToArray(), stored.Version);
}
