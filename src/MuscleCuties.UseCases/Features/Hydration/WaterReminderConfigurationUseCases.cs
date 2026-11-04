using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

public sealed class GetWaterReminderConfigurationUseCase(IWaterReminderConfigurationStore store)
{
    public async Task<WaterReminderConfigurationOutputDto> ExecuteAsync(GetWaterReminderConfigurationInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WaterReminderConfigurationOperation.Map(await WaterReminderConfigurationOperation.ReadAsync(store,
            input.AccountId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class SaveWaterReminderConfigurationUseCase(IWaterReminderConfigurationStore store)
{
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

internal static class WaterReminderConfigurationOperation
{
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

    internal static void RequireScope(StoredWaterReminderConfiguration stored, Guid accountId)
    {
        if (stored.Configuration.AccountId != accountId)
            throw new InvalidOperationException("Reminder storage returned a different account.");
    }

    internal static WaterReminderConfigurationOutputDto Map(StoredWaterReminderConfiguration stored) => new(
        stored.Configuration.AccountId, stored.Version, stored.Configuration.Settings.Enabled,
        stored.Configuration.Settings.LocalTimes, stored.Configuration.Settings.QuietStart, stored.Configuration.Settings.QuietEnd);
}
