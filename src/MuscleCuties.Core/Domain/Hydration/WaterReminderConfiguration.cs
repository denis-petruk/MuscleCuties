namespace MuscleCuties.Core.Domain.Hydration;

public sealed record WaterReminderConfigurationChange(Guid OperationId, FixedWaterReminderSettings Settings);

/// <summary>Server-owned wall-clock intent; device permission and delivery state are excluded.</summary>
public sealed class WaterReminderConfiguration
{
    public const int MaximumOperations = 4096;

    private WaterReminderConfiguration(Guid accountId, FixedWaterReminderSettings settings, WaterReminderConfigurationChange[] operations)
    {
        AccountId = accountId;
        Settings = settings;
        Operations = Array.AsReadOnly(operations);
    }

    public Guid AccountId { get; }
    public FixedWaterReminderSettings Settings { get; }
    public IReadOnlyList<WaterReminderConfigurationChange> Operations { get; }

    public static WaterReminderConfiguration Create(Guid accountId, bool optedIn,
        IEnumerable<TimeOnly>? times, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        return new(accountId, optedIn ? FixedWaterReminderSettings.Enable(times, quietStart, quietEnd)
            : FixedWaterReminderSettings.Disabled(), []);
    }

    public static WaterReminderConfiguration Restore(Guid accountId, FixedWaterReminderSettings settings,
        IEnumerable<WaterReminderConfigurationChange> operations)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(operations);
        var receipts = operations.Take(MaximumOperations + 1).ToArray();
        if (receipts.Length > MaximumOperations || receipts.Any(o => o is null || o.OperationId == Guid.Empty || o.Settings is null) ||
            receipts.Select(o => o.OperationId).Distinct().Count() != receipts.Length)
            throw new ArgumentException("Reminder operation receipts must be unique and bounded.", nameof(operations));
        return new(accountId, settings, receipts);
    }

    public WaterReminderConfiguration Change(Guid operationId, bool optedIn, IEnumerable<TimeOnly>? times,
        TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("A durable operation identity is required.", nameof(operationId));
        var updated = Create(AccountId, optedIn, times, quietStart, quietEnd).Settings;
        var previous = Operations.SingleOrDefault(o => o.OperationId == operationId);
        if (previous is not null)
        {
            if (previous.Settings.Enabled != updated.Enabled || previous.Settings.QuietStart != updated.QuietStart ||
                previous.Settings.QuietEnd != updated.QuietEnd || !previous.Settings.LocalTimes.SequenceEqual(updated.LocalTimes))
                throw new InvalidOperationException("Operation identity was reused for a different reminder configuration.");
            return this;
        }
        return Restore(AccountId, updated, Operations.Append(new(operationId, updated)));
    }
}
