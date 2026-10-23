namespace MuscleCuties.Core.Domain.Hydration;

public enum WaterReminderAuthorizationState { Unknown, Denied, Granted }

/// <summary>Local wall-clock intent only. This type never claims an OS alert was scheduled.</summary>
public sealed class FixedWaterReminderSettings
{
    public const int MaximumReminderTimes = 24;

    private FixedWaterReminderSettings(bool enabled, TimeOnly[] times, TimeOnly? quietStart, TimeOnly? quietEnd)
    {
        Enabled = enabled;
        LocalTimes = Array.AsReadOnly(times);
        QuietStart = quietStart;
        QuietEnd = quietEnd;
    }

    public bool Enabled { get; }
    public IReadOnlyList<TimeOnly> LocalTimes { get; }
    public TimeOnly? QuietStart { get; }
    public TimeOnly? QuietEnd { get; }
    public static FixedWaterReminderSettings Disabled() => new(false, [], null, null);

    // Owner decision (2026-10-20): the user explicitly chooses reminder times and quiet hours.
    // Start is inclusive, end exclusive; overnight windows are supported, equal endpoints rejected.
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

    public bool CanRequestScheduling(WaterReminderAuthorizationState permission) =>
        Enabled && permission == WaterReminderAuthorizationState.Granted;
}
