namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>One change to the reminder settings.</summary>
public sealed record WaterReminderConfigurationChange(Guid OperationId, FixedWaterReminderSettings Settings);

/// <summary>Server-owned wall-clock intent; device permission and delivery state are excluded.</summary>
public sealed class WaterReminderConfiguration
{
    public const int MaximumOperations = 4096;

    /// <summary>The reminder settings in force, with the times the scheduler uses.</summary>
    private WaterReminderConfiguration(Guid accountId, FixedWaterReminderSettings settings, WaterReminderConfigurationChange[] operations)
    {
        AccountId = accountId;
        Settings = settings;
        Operations = Array.AsReadOnly(operations);
    }

    /// <summary>Account that owns the reminder settings.</summary>
    public Guid AccountId { get; }
    /// <summary>Reminder times in force, or the disabled settings.</summary>
    public FixedWaterReminderSettings Settings { get; }
    /// <summary>Durable changes applied to the settings, in order.</summary>
    public IReadOnlyList<WaterReminderConfigurationChange> Operations { get; }

    /// <summary>Starts a configuration from the opt-in choice and the reminder times.</summary>
    public static WaterReminderConfiguration Create(Guid accountId, bool optedIn,
        IEnumerable<TimeOnly>? times, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        return new(accountId, optedIn ? FixedWaterReminderSettings.Enable(times, quietStart, quietEnd)
            : FixedWaterReminderSettings.Disabled(), []);
    }

    /// <summary>Rebuilds a configuration from stored settings and its applied changes.</summary>
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

    /// <summary>Applies a durable change to the opt-in flag, times and quiet window.</summary>
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
