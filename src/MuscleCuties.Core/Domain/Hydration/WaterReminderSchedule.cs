namespace MuscleCuties.Core.Domain.Hydration;

public sealed record WaterReminderCandidate(Guid AccountId, DateOnly LocalDate, TimeOnly LocalTime, DateTimeOffset Instant);

/// <summary>
/// Follows the current device timezone while retaining user-selected wall-clock times.
/// Skip DST gaps; use the earlier instant of a fold exactly once, never its later duplicate.
/// Candidates are future intent, never evidence of a scheduled/delivered notification.
/// </summary>
public static class WaterReminderSchedule
{
    public static IReadOnlyList<WaterReminderCandidate> ForDate(WaterReminderConfiguration configuration,
        DateOnly localDate, TimeZoneInfo deviceTimeZone, DateTimeOffset now, WaterReminderAuthorizationState permission)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(deviceTimeZone);
        if (!configuration.Settings.CanRequestScheduling(permission)) return [];
        var candidates = new List<WaterReminderCandidate>();
        foreach (var time in configuration.Settings.LocalTimes)
        {
            var wallClock = localDate.ToDateTime(time, DateTimeKind.Unspecified);
            if (deviceTimeZone.IsInvalidTime(wallClock)) continue;
            var offset = deviceTimeZone.IsAmbiguousTime(wallClock)
                ? deviceTimeZone.GetAmbiguousTimeOffsets(wallClock).Max()
                : deviceTimeZone.GetUtcOffset(wallClock);
            // Boundary calendar dates can fall outside DateTimeOffset's representable UTC range.
            var utcTicks = wallClock.Ticks - offset.Ticks;
            if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks) continue;
            var instant = new DateTimeOffset(wallClock, offset).ToUniversalTime();
            if (instant > now) candidates.Add(new(configuration.AccountId, localDate, time, instant));
        }
        return candidates.AsReadOnly();
    }
}
