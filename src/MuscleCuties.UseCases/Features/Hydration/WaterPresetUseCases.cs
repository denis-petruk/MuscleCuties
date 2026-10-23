using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

// AccountId must be resolved from the authenticated principal by the server, never trusted from a UI request.
public sealed class GetWaterPresetsUseCase(IWaterPresetStore store)
{
    public async Task<WaterPresetCatalogOutputDto> ExecuteAsync(GetWaterPresetsInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stored = await WaterPresetOperation.ReadAsync(store, input.AccountId, cancellationToken).ConfigureAwait(false);
        return WaterPresetOperation.Map(stored);
    }
}

public sealed class CreateWaterPresetUseCase(IWaterPresetStore store)
{
    public Task<WaterPresetCatalogOutputDto> ExecuteAsync(CreateWaterPresetInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterPresetOperation.ChangeAsync(store, input.AccountId, input.ExpectedVersion,
            WaterPresetMutation.Create(input.OperationId, input.Label, input.Millilitres), cancellationToken);
    }
}

public sealed class UpdateWaterPresetUseCase(IWaterPresetStore store)
{
    public Task<WaterPresetCatalogOutputDto> ExecuteAsync(UpdateWaterPresetInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterPresetOperation.ChangeAsync(store, input.AccountId, input.ExpectedVersion,
            WaterPresetMutation.Update(input.OperationId, input.PresetId, input.Label, input.Millilitres), cancellationToken);
    }
}

public sealed class DeleteWaterPresetUseCase(IWaterPresetStore store)
{
    public Task<WaterPresetCatalogOutputDto> ExecuteAsync(DeleteWaterPresetInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterPresetOperation.ChangeAsync(store, input.AccountId, input.ExpectedVersion,
            WaterPresetMutation.Delete(input.OperationId, input.PresetId), cancellationToken);
    }
}

public sealed class ReorderWaterPresetsUseCase(IWaterPresetStore store)
{
    public Task<WaterPresetCatalogOutputDto> ExecuteAsync(ReorderWaterPresetsInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterPresetOperation.ChangeAsync(store, input.AccountId, input.ExpectedVersion,
            WaterPresetMutation.Reorder(input.OperationId, input.OrderedIds), cancellationToken);
    }
}

internal static class WaterPresetOperation
{
    internal static async Task<StoredWaterPresetCatalog> ReadAsync(IWaterPresetStore store, Guid accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        // Domain validates the account even if a faulty store would accept an empty identity.
        var empty = WaterPresetCatalog.Restore(accountId, []);
        var stored = await store.ReadAsync(empty.AccountId, cancellationToken).ConfigureAwait(false);
        if (stored.Catalog.AccountId != accountId)
            throw new InvalidOperationException("Preset storage returned a different account.");
        return stored;
    }

    internal static async Task<WaterPresetCatalogOutputDto> ChangeAsync(IWaterPresetStore store, Guid accountId,
        string? expectedVersion, WaterPresetMutation mutation, CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(store, accountId, cancellationToken).ConfigureAwait(false);
        var updated = stored.State.Apply(mutation);
        if (ReferenceEquals(updated, stored.State)) return Map(stored);
        if (!string.Equals(stored.ConcurrencyToken, expectedVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Presets changed. Reload before editing.");
        cancellationToken.ThrowIfCancellationRequested();
        var committed = await store.TryReplaceAsync(updated, stored.ConcurrencyToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Presets changed during this operation. Reload before editing again.");
        if (committed.Catalog.AccountId != accountId)
            throw new InvalidOperationException("Preset storage returned a different account.");
        return Map(committed);
    }

    internal static WaterPresetCatalogOutputDto Map(StoredWaterPresetCatalog stored) =>
        new(stored.Catalog.AccountId, stored.ConcurrencyToken,
            stored.Catalog.Presets.Select(p => new WaterPresetOutputDto(p.Id, p.Label, p.Portion.Millilitres)).ToArray());
}
