using MuscleCuties.Core.Domain.Hydration;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>Atomically seed once per account using a durable initialization marker, including empty/deleted catalogs.</summary>
public interface IWaterPresetInitializationStore : IWaterPresetStore
{
    // False means previously initialized (or has existing custom presets); never merge or resurrect defaults.
    // The adapter must return durable state from ReadAsync after either outcome and preserve stable stored IDs.
    Task<bool> TryInitializeAsync(WaterPresetCatalog defaults, CancellationToken cancellationToken);
}

/// <summary>One stored intake day with the operations that produced it.</summary>
public sealed record StoredWaterIntakeDay(WaterIntakeDay Day, string? Version);
/// <summary>The intake persistence, storing operations rather than a running total.</summary>
public interface IWaterIntakeStore
{
    // PostgreSQL authority. Fence both account and date; an absent day returns an empty ledger and null version.
    Task<StoredWaterIntakeDay> ReadAsync(Guid accountId, DateOnly localDate, CancellationToken cancellationToken);
    // Atomic CAS must write totals, entries AND operation receipts in the same transaction. Null means conflict.
    // Existing legacy totals become the baseline, never counted twice or modified by these entry changes.
    Task<StoredWaterIntakeDay?> TryReplaceAsync(WaterIntakeDay day, string? expectedVersion, CancellationToken cancellationToken);
}

/// <summary>The stored reminder settings with the operations behind them.</summary>
public sealed record StoredWaterReminderConfiguration(WaterReminderConfiguration Configuration, string? Version);
/// <summary>The reminder persistence.</summary>
public interface IWaterReminderConfigurationStore
{
    // First-party PostgreSQL, account fenced. Absent configuration is disabled with null version.
    Task<StoredWaterReminderConfiguration> ReadAsync(Guid accountId, CancellationToken cancellationToken);
    // Atomic CAS of configuration AND operation receipts, including absent-row creation; null means conflict.
    // Return acknowledged configuration/version. Preserve receipts across restart and logout; purge only with account data.
    Task<StoredWaterReminderConfiguration?> TryReplaceAsync(WaterReminderConfiguration configuration,
        string? expectedVersion, CancellationToken cancellationToken);
}
