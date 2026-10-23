namespace MuscleCuties.Core.Domain.Workout;

public static class WorkoutScheduleMathRule
{
    public static readonly DateTime MinSupportedDate = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime MaxSupportedDate = new(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    public static void EnsureValidReschedule(int workoutDayId, DateOnly targetDate)
    {
        if (workoutDayId <= 0 || targetDate.Year < MinSupportedDate.Year ||
            targetDate.Year > MaxSupportedDate.Year)
            throw new ArgumentException("Invalid schedule change.");
    }

    public static DateTime ClampSafeDate(DateTime date)
    {
        if (date < MinSupportedDate) return MinSupportedDate;
        if (date > MaxSupportedDate) return MaxSupportedDate;
        return date;
    }

    public static DateTime GetSafeWeekStart(DateTime date)
    {
        var safe = ClampSafeDate(date);
        var diff = (7 + (int)safe.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return SafeAddDays(safe.Date, -diff);
    }

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
