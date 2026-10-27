namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Calendar calculations use the latest server-accepted foreground timezone.</summary>
public static class WorkoutAccountCalendarRule
{
    public const int InitialTimeZoneVersion = 0;

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    /// <summary>Background reports and replayed versions cannot change account scheduling.</summary>
    public static bool CanAcceptTimeZoneUpdate(bool isForeground, long expectedVersion, long currentVersion,
        string? timeZoneId)
    {
        if (!isForeground || currentVersion < InitialTimeZoneVersion || expectedVersion != currentVersion ||
            currentVersion == long.MaxValue || string.IsNullOrWhiteSpace(timeZoneId))
            return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    /// <summary>
    /// Compares local dates rather than elapsed hours, so DST gaps and repeated hours do not
    /// shorten or extend a calendar window. Callers retain the originally credited local date.
    /// </summary>
    public static bool HasReachedLocalDate(DateTimeOffset now, TimeZoneInfo timeZone, DateOnly boundary) =>
        LocalDate(now, timeZone) >= boundary;
}
