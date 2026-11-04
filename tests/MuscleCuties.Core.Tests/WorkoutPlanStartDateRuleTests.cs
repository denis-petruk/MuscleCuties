using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public class WorkoutPlanStartDateRuleTests
{
    [Fact]
    public void Selected_today_is_eligible_when_no_activity_was_completed()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 26), WorkoutPlanStartDateRule.FindNextEligibleDate(
            now, TimeZoneInfo.Utc, [DayOfWeek.Monday, DayOfWeek.Wednesday], false));
    }

    [Fact]
    public void Completed_today_moves_to_next_selected_day()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 28), WorkoutPlanStartDateRule.FindNextEligibleDate(
            now, TimeZoneInfo.Utc, [DayOfWeek.Monday, DayOfWeek.Wednesday], true));
    }

    [Fact]
    public void Converts_instant_to_account_local_date_across_dst()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
        var now = new DateTimeOffset(2026, 3, 8, 4, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 3, 7), WorkoutPlanStartDateRule.FindNextEligibleDate(
            now, zone, [DayOfWeek.Saturday, DayOfWeek.Sunday], false));
    }

    [Fact]
    public void Missing_or_invalid_selection_returns_no_date()
    {
        Assert.Null(WorkoutPlanStartDateRule.FindNextEligibleDate(DateTimeOffset.UtcNow,
            TimeZoneInfo.Utc, [(DayOfWeek)99], false));
        Assert.Null(WorkoutPlanStartDateRule.FindNextEligibleDate(DateTimeOffset.UtcNow,
            null, [DayOfWeek.Monday], false));
    }
}
