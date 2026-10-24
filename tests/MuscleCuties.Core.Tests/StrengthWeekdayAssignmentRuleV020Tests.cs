using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthWeekdayAssignmentRuleV020Tests
{
    [Fact]
    public void Unreviewed_Features_Default_Off_Independently()
    {
        Assert.False(WorkoutTrainingPolicyV020.TrainingPrescriptionPreviewEnabled);
        Assert.False(WorkoutTrainingPolicyV020.CheckInScoreDisplayEnabled);
        Assert.False(WorkoutTrainingPolicyV020.ReadinessSuggestionsEnabled);
        Assert.False(WorkoutTrainingPolicyV020.AutomatedProgressionEnabled);
        Assert.False(WorkoutTrainingPolicyV020.InjurySpecificSubstitutionsEnabled);
    }

    [Fact]
    public void Assigns_Ordered_Sessions_To_User_Selected_Available_Weekdays()
    {
        var result = StrengthWeekdayAssignmentRuleV020.Assign(3,
            [DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday],
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday });

        Assert.Equal("Assigned", result.Status);
        Assert.Equal([
            new StrengthWeekdayAssignmentV020(0, DayOfWeek.Monday),
            new StrengthWeekdayAssignmentV020(1, DayOfWeek.Wednesday),
            new StrengthWeekdayAssignmentV020(2, DayOfWeek.Friday)
        ], result.Assignments);
        Assert.Null(result.LimitationCode);
    }

    [Fact]
    public void Does_Not_Default_When_Selected_Day_Is_Unavailable()
    {
        var result = StrengthWeekdayAssignmentRuleV020.Assign(2,
            [DayOfWeek.Monday, DayOfWeek.Friday],
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Thursday });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Assignments);
        Assert.Equal("StrengthWeekdaySelectionIncludesUnavailableDay", result.LimitationCode);
    }

    [Fact]
    public void Requires_One_Distinct_Weekday_Per_Proposal_Session()
    {
        var result = StrengthWeekdayAssignmentRuleV020.Assign(2,
            [DayOfWeek.Monday, DayOfWeek.Monday],
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Assignments);
        Assert.Equal("StrengthWeekdaySelectionContainsDuplicates", result.LimitationCode);
    }
}
