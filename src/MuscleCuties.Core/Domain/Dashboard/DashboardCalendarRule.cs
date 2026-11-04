namespace MuscleCuties.Core.Domain.Dashboard;

public static class DashboardCalendarRule
{
    public const int MinimumSupportedYear = 1970;
    public const int MaximumSupportedYear = 2099;
    public const int FirstMonthOfYear = 1;
    public const int LastMonthOfYear = 12;

    public static bool IsValidMonth(int year, int month) =>
        year is >= MinimumSupportedYear and <= MaximumSupportedYear &&
        month is >= FirstMonthOfYear and <= LastMonthOfYear;
}
