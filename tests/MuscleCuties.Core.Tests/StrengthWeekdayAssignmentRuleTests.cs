using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthWeekdayAssignmentRuleTests
{
    private const int ThreeStrengthSessionsPerWeek = 3;
    private const int TwoStrengthSessionsPerWeek = 2;
    private const int OneStrengthSessionPerWeek = StrengthWeekdayAssignmentRule.MinimumSessionsPerWeek;
    private const int FirstSessionIndex = 0;
    private const int SecondSessionIndex = FirstSessionIndex + 1;
    private const int ThirdSessionIndex = SecondSessionIndex + 1;

    [Fact]
    public void SpreadsSessionsAcrossAvailableWeekdays()
    {
        var availableWeekdays = Enum.GetValues<DayOfWeek>().ToHashSet();

        var result = StrengthWeekdayAssignmentRule.Assign(ThreeStrengthSessionsPerWeek, availableWeekdays);

        Assert.Equal(StrengthWeekdayAssignmentStatus.Assigned, result.Status);
        Assert.Equal(new[]
        {
            new StrengthWeekdayAssignment(FirstSessionIndex, DayOfWeek.Monday),
            new StrengthWeekdayAssignment(SecondSessionIndex, DayOfWeek.Wednesday),
            new StrengthWeekdayAssignment(ThirdSessionIndex, DayOfWeek.Friday)
        }, result.Assignments);
        Assert.Null(result.LimitationCode);
    }

    [Fact]
    public void LimitsWhenSessionsExceedAvailableWeekdays()
    {
        var result = StrengthWeekdayAssignmentRule.Assign(ThreeStrengthSessionsPerWeek,
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday });

        Assert.Equal(StrengthWeekdayAssignmentStatus.Limited, result.Status);
        Assert.Empty(result.Assignments);
        Assert.Equal("StrengthSessionsExceedAvailableWeekdays", result.LimitationCode);
    }

    [Fact]
    public void RejectsInvalidWeekdayInput()
    {
        var result = StrengthWeekdayAssignmentRule.Assign(OneStrengthSessionPerWeek,
            new HashSet<DayOfWeek> { (DayOfWeek)WorkoutPreferencesRule.MaximumDaysPerWeek });

        Assert.Equal(StrengthWeekdayAssignmentStatus.Limited, result.Status);
        Assert.Empty(result.Assignments);
        Assert.Equal("StrengthWeekdayAssignmentInvalid", result.LimitationCode);
    }

    [Fact]
    public void AssignBySpecification_SingleSession_TwoAvailableDays_PicksFirstDay()
    {
        var days = new HashSet<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday };
        var result = StrengthWeekdayAssignmentRule.AssignBySpecification(OneStrengthSessionPerWeek, days);

        Assert.Equal(StrengthWeekdayAssignmentStatus.Assigned, result.Status);
        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(DayOfWeek.Tuesday, assignment.Weekday);
    }

    [Fact]
    public void AssignBySpecification_SingleSession_ThreeAvailableDays_PicksMiddleDay()
    {
        var days = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var result = StrengthWeekdayAssignmentRule.AssignBySpecification(OneStrengthSessionPerWeek, days);

        Assert.Equal(StrengthWeekdayAssignmentStatus.Assigned, result.Status);
        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(DayOfWeek.Wednesday, assignment.Weekday);
    }

    [Fact]
    public void AssignBySpecification_TwoSessions_PicksMaximalSpacing()
    {
        var days = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var result = StrengthWeekdayAssignmentRule.AssignBySpecification(TwoStrengthSessionsPerWeek, days);

        Assert.Equal(StrengthWeekdayAssignmentStatus.Assigned, result.Status);
        Assert.Equal(new[]
        {
            new StrengthWeekdayAssignment(FirstSessionIndex, DayOfWeek.Monday),
            new StrengthWeekdayAssignment(SecondSessionIndex, DayOfWeek.Friday)
        }, result.Assignments);
    }

    [Fact]
    public void AssignBySpecification_ThreeSessions_FiveDays_SpreadsByCalculatedIndex()
    {
        var days = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        var result = StrengthWeekdayAssignmentRule.AssignBySpecification(ThreeStrengthSessionsPerWeek, days);

        Assert.Equal(StrengthWeekdayAssignmentStatus.Assigned, result.Status);
        Assert.Equal(new[]
        {
            new StrengthWeekdayAssignment(FirstSessionIndex, DayOfWeek.Monday),
            new StrengthWeekdayAssignment(SecondSessionIndex, DayOfWeek.Wednesday),
            new StrengthWeekdayAssignment(ThirdSessionIndex, DayOfWeek.Friday)
        }, result.Assignments);
    }
}
