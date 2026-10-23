using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

public sealed class GetWaterIntakeUseCase(IWaterIntakeStore store)
{
    public async Task<WaterIntakeDayOutputDto> ExecuteAsync(GetWaterIntakeInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterIntakeOperation.Map(await WaterIntakeOperation.ReadAsync(store, input.AccountId, input.Date, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class ChangeWaterIntakeUseCase(IWaterIntakeStore store)
{
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

internal static class WaterIntakeOperation
{
    internal static async Task<StoredWaterIntakeDay> ReadAsync(IWaterIntakeStore store, Guid accountId, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        var empty = WaterIntakeDay.Restore(accountId, date, 0, [], []);
        var stored = await store.ReadAsync(empty.AccountId, date, cancellationToken).ConfigureAwait(false);
        RequireScope(stored, accountId, date);
        return stored;
    }

    internal static void RequireScope(StoredWaterIntakeDay stored, Guid accountId, DateOnly date)
    {
        if (stored.Day.AccountId != accountId || stored.Day.Date != date)
            throw new InvalidOperationException("Intake storage returned a different account or date.");
    }

    internal static WaterIntakeDayOutputDto Map(StoredWaterIntakeDay stored) => new(stored.Day.AccountId,
        stored.Day.Date, stored.Day.LegacyMillilitres, stored.Day.TotalMillilitres,
        stored.Day.Entries.Select(e => new WaterIntakeEntryOutputDto(e.Id, e.Millilitres, e.Undone)).ToArray(), stored.Version);
}
