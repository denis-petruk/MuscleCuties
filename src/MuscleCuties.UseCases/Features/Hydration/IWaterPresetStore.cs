using MuscleCuties.Core.Domain.Hydration;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>Her preset catalogue with the operations that produced it.</summary>
public sealed record StoredWaterPresetCatalog(WaterPresetState State, string? ConcurrencyToken)
{
    /// <summary>The catalog the stored state currently holds.</summary>
    public WaterPresetCatalog Catalog => State.Catalog;
}

/// <summary>
/// First-party PostgreSQL adapter contract. Every operation must fence the authenticated account.
/// Read returns an empty catalog for a new account. Replace atomically checks its token (including
/// absent-row creation), replaces ordered catalog AND operation receipts in one transaction, and returns null on conflict. Never retry
/// an edit automatically. Persistence failures propagate; successful DTOs follow durable writes only.
/// </summary>
public interface IWaterPresetStore
{
    Task<StoredWaterPresetCatalog> ReadAsync(Guid accountId, CancellationToken cancellationToken);
    Task<StoredWaterPresetCatalog?> TryReplaceAsync(WaterPresetState state, string? expectedConcurrencyToken, CancellationToken cancellationToken);
}
