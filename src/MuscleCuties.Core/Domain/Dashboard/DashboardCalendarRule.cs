namespace MuscleCuties.Core.Domain.Dashboard;

/// <summary>Builds the calendar strip, keeping its days in one contiguous, ordered range.</summary>
public static class DashboardCalendarRule
{
    public const int MinimumSupportedYear = 1970;
    public const int MaximumSupportedYear = 2099;
    public const int FirstMonthOfYear = 1;
    public const int LastMonthOfYear = 12;

    /// <summary>True when the year and month form a calendar month the dashboard can render.</summary>
    public static bool IsValidMonth(int year, int month) =>
        year is >= MinimumSupportedYear and <= MaximumSupportedYear &&
        month is >= FirstMonthOfYear and <= LastMonthOfYear;
}
