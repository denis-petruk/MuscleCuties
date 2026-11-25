namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Whether she allowed reminders, which the scheduler checks before any is sent.</summary>
public enum WaterReminderAuthorizationState { Unknown, Denied, Granted }

/// <summary>Local wall-clock intent only. This type never claims an OS alert was scheduled.</summary>
public sealed class FixedWaterReminderSettings
{
    public const int MaximumReminderTimes = 24;

    /// <summary>The reminder settings, which the app keeps at fixed times rather than per-account ones.</summary>
    private FixedWaterReminderSettings(bool enabled, TimeOnly[] times, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        Enabled = enabled;
        LocalTimes = Array.AsReadOnly(times);
        QuietStart = quietStart;
        QuietEnd = quietEnd;
    }

    /// <summary>True when fixed-time reminders are switched on.</summary>
    public bool Enabled { get; }
    /// <summary>Times of day, on the user's local clock, at which a reminder fires.</summary>
    public IReadOnlyList<TimeOnly> LocalTimes { get; }
    /// <summary>Start of the quiet window in which reminders are suppressed.</summary>
    public TimeOnly? QuietStart { get; }
    /// <summary>End of the quiet window in which reminders are suppressed.</summary>
    public TimeOnly? QuietEnd { get; }
    /// <summary>The settings that schedule no reminders.</summary>
    public static FixedWaterReminderSettings Disabled() => new(false, [], null, null);

    // Owner decision (2026-10-20): the user explicitly chooses reminder times and quiet hours.
    // Start is inclusive, end exclusive; overnight windows are supported, equal endpoints rejected.
    /// <summary>Turns the reminders on only when the authorization allows it.</summary>
    public static FixedWaterReminderSettings Enable(IEnumerable<TimeOnly>? times, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        ArgumentNullException.ThrowIfNull(times);
        if (!quietStart.HasValue || !quietEnd.HasValue)
            throw new ArgumentException("Choose quiet hours explicitly.", nameof(quietStart));
        var localTimes = times.Take(MaximumReminderTimes + 1).Order().ToArray();
        if (localTimes.Length == 0 || localTimes.Length > MaximumReminderTimes ||
            localTimes.Distinct().Count() != localTimes.Length || quietStart == quietEnd ||
            localTimes.Any(t => quietStart < quietEnd
                ? t >= quietStart && t < quietEnd
                : t >= quietStart || t < quietEnd))
            throw new ArgumentException("Choose distinct times outside an explicit nonempty quiet window.", nameof(times));
        return new FixedWaterReminderSettings(true, localTimes, quietStart, quietEnd);
    }

    /// <summary>True when the permission state allows asking the platform to schedule reminders.</summary>
    public bool CanRequestScheduling(WaterReminderAuthorizationState permission) =>
        Enabled && permission == WaterReminderAuthorizationState.Granted;
}
