namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Date arithmetic for rescheduling, keeping every computed date inside the supported range.</summary>
public static class WorkoutScheduleMathRule
{
    public static readonly DateTime MinSupportedDate = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MaxSupportedDate = new(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>True when the date is inside the range the scheduler covers.</summary>
    public static bool IsSupportedDate(DateOnly date) =>
        date.Year >= MinSupportedDate.Year && date.Year <= MaxSupportedDate.Year;

    /// <summary>Refuses a reschedule of an unknown day or to an unsupported date.</summary>
    public static void EnsureValidReschedule(int workoutDayId, DateOnly targetDate)
    {
        if (workoutDayId <= 0 || targetDate.Year < MinSupportedDate.Year ||
            targetDate.Year > MaxSupportedDate.Year)
            throw new ArgumentException("Invalid schedule change.");
    }

    /// <summary>Clamps a date into the supported range, so a shift cannot leave it.</summary>
    public static DateTime ClampSafeDate(DateTime date)
    {
        if (date < MinSupportedDate) return MinSupportedDate;
        if (date > MaxSupportedDate) return MaxSupportedDate;
        return date;
    }

    /// <summary>The clamped start of the week, used as the base for shifts.</summary>
    public static DateTime GetSafeWeekStart(DateTime date)
    {
        var safe = ClampSafeDate(date);
        var diff = (7 + (int)safe.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return SafeAddDays(safe.Date, -diff);
    }

    /// <summary>Adds days with the range applied, so a long shift cannot overflow.</summary>
    public static DateTime SafeAddDays(DateTime baseDate, int days)
    {
        try
        {
            if (days > 0 && baseDate > DateTime.MaxValue.AddDays(-days))
                return MaxSupportedDate;
            if (days < 0 && baseDate < DateTime.MinValue.AddDays(-days))
                return MinSupportedDate;
            return ClampSafeDate(baseDate.AddDays(days));
        }
        catch (ArgumentOutOfRangeException)
        {
            return days >= 0 ? MaxSupportedDate : MinSupportedDate;
        }
    }

    /// <summary>The day's label in her language, falling back to the stored label.</summary>
    public static string FormatDayLabel(DayOfWeek dayOfWeek, string fallback = "DAY")
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "SUN",
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => fallback
        };
    }
}
