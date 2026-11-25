using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>The reminder settings in force.</summary>
public sealed class GetWaterReminderConfigurationUseCase(IWaterReminderConfigurationStore store)
{
    /// <summary>Reads the account's reminder configuration.</summary>
    public async Task<WaterReminderConfigurationOutputDto> ExecuteAsync(GetWaterReminderConfigurationInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterReminderConfigurationOperation.Map(await WaterReminderConfigurationOperation.ReadAsync(store,
            input.AccountId, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Stores reminder settings, refusing times outside the quiet window rules.</summary>
public sealed class SaveWaterReminderConfigurationUseCase(IWaterReminderConfigurationStore store)
{
    /// <summary>Saves the reminder configuration and returns what was stored.</summary>
    public async Task<WaterReminderConfigurationOutputDto> ExecuteAsync(SaveWaterReminderConfigurationInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stored = await WaterReminderConfigurationOperation.ReadAsync(store, input.AccountId, cancellationToken).ConfigureAwait(false);
        var updated = stored.Configuration.Change(input.OperationId, input.OptedIn, input.LocalTimes, input.QuietStart, input.QuietEnd);
        if (ReferenceEquals(updated, stored.Configuration)) return WaterReminderConfigurationOperation.Map(stored);
        if (!string.Equals(stored.Version, input.ExpectedVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Reminder settings changed. Reload before saving.");
        cancellationToken.ThrowIfCancellationRequested();
        var committed = await store.TryReplaceAsync(updated, stored.Version, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Reminder settings changed during persistence. Reload before saving.");
        WaterReminderConfigurationOperation.RequireScope(committed, input.AccountId);
        return WaterReminderConfigurationOperation.Map(committed);
    }
}

/// <summary>Builds the stored operation for a reminder change.</summary>
internal static class WaterReminderConfigurationOperation
{
    /// <summary>Reads the stored configuration, falling back to defaults.</summary>
    internal static async Task<StoredWaterReminderConfiguration> ReadAsync(IWaterReminderConfigurationStore store,
        Guid accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        var empty = WaterReminderConfiguration.Create(accountId, false, null, null, null);
        var stored = await store.ReadAsync(empty.AccountId, cancellationToken).ConfigureAwait(false);
        RequireScope(stored, accountId);
        return stored;
    }

    /// <summary>Refuses stored settings that belong to another account.</summary>
    internal static void RequireScope(StoredWaterReminderConfiguration stored, Guid accountId)
    {
        if (stored.Configuration.AccountId != accountId)
            throw new InvalidOperationException("Reminder storage returned a different account.");
    }

    /// <summary>Maps a stored configuration onto its output shape.</summary>
    internal static WaterReminderConfigurationOutputDto Map(StoredWaterReminderConfiguration stored) => new(
        stored.Configuration.AccountId, stored.Version, stored.Configuration.Settings.Enabled,
        stored.Configuration.Settings.LocalTimes, stored.Configuration.Settings.QuietStart, stored.Configuration.Settings.QuietEnd);
}
