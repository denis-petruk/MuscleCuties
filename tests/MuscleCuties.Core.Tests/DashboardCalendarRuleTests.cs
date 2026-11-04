using MuscleCuties.Core.Domain.Dashboard;

namespace MuscleCuties.Core.Tests;

public sealed class DashboardCalendarRuleTests
{
    [Theory]
    [InlineData(DashboardCalendarRule.MinimumSupportedYear, DashboardCalendarRule.FirstMonthOfYear, true)]
    [InlineData(DashboardCalendarRule.MaximumSupportedYear, DashboardCalendarRule.LastMonthOfYear, true)]
    [InlineData(DashboardCalendarRule.MinimumSupportedYear - 1, DashboardCalendarRule.FirstMonthOfYear, false)]
    [InlineData(DashboardCalendarRule.MaximumSupportedYear + 1, DashboardCalendarRule.FirstMonthOfYear, false)]
    [InlineData(DashboardCalendarRule.MinimumSupportedYear, DashboardCalendarRule.FirstMonthOfYear - 1, false)]
    [InlineData(DashboardCalendarRule.MinimumSupportedYear, DashboardCalendarRule.LastMonthOfYear + 1, false)]
    public void Month_Is_Valid_Only_Within_Supported_Year_And_Month_Bounds(
        int year, int month, bool expected)
    {
        Assert.Equal(expected, DashboardCalendarRule.IsValidMonth(year, month));
    }
}
