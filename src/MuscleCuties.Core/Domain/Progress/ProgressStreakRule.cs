namespace MuscleCuties.Core.Domain.Progress;

public static class ProgressStreakRule
{
    public static DateOnly GetLookbackStart(DateOnly today, int lookbackDays)
    {
        var days = Math.Max(1L, lookbackDays);
        var startDayNumber = Math.Max(DateOnly.MinValue.DayNumber,
            today.DayNumber - days + 1);
        return DateOnly.FromDayNumber((int)startDayNumber);
    }

    public static int CountCurrentStreak(IEnumerable<DateOnly> dates, DateOnly today)
    {
        var loggedDayNumbers = dates.Select(date => date.DayNumber).ToHashSet();
        var day = loggedDayNumbers.Contains(today.DayNumber)
            ? today.DayNumber
            : today.DayNumber - 1;
        var streak = 0;

        while (day >= DateOnly.MinValue.DayNumber && loggedDayNumbers.Contains(day))
        {
            streak++;
            day--;
        }

        return streak;
    }
}
