using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutAccountCalendarRuleTests
{
    private const long AcceptedVersion = 4;
    private const long StaleVersion = 3;
    private const string UtcZoneId = "UTC";

    [Fact]
    public void ForegroundUpdateRequiresCurrentVersion()
    {
        Assert.True(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(true, AcceptedVersion, AcceptedVersion, UtcZoneId));
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(false, AcceptedVersion, AcceptedVersion, UtcZoneId));
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(true, StaleVersion, AcceptedVersion, UtcZoneId));
        Assert.False(WorkoutAccountCalendarRule.CanAcceptTimeZoneUpdate(true, AcceptedVersion, AcceptedVersion, "Synthetic/Unknown"));
    }

    [Theory]
    [InlineData("2026-03-08T06:59:00Z", "2026-03-08")]
    [InlineData("2026-03-08T07:01:00Z", "2026-03-08")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01")]
    [InlineData("2026-11-02T04:59:00Z", "2026-11-01")]
    [InlineData("2026-11-02T05:00:00Z", "2026-11-02")]
    public void DstAndMidnightResolveByAccountLocalDate(string instant, string expectedDate)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
        Assert.Equal(DateOnly.Parse(expectedDate), WorkoutAccountCalendarRule.LocalDate(DateTimeOffset.Parse(instant), zone));
    }
}
