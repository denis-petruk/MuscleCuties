using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutEffortRuleTests
{
    [Fact]
    public void Missing_Effort_Remains_Unset_Until_Slider_Changes()
    {
        Assert.False(WorkoutEffortRule.TryParseLogged(null, out _));
        Assert.False(WorkoutEffortRule.TryParseLogged(string.Empty, out _));
        Assert.Equal(5, WorkoutEffortRule.NeutralSliderPosition);
    }

    [Fact]
    public void Slider_Only_Produces_Whole_In_Range_Effort()
    {
        Assert.True(WorkoutEffortRule.TryRoundSlider(4.2, out var rounded));
        Assert.Equal(4, rounded);

        Assert.True(WorkoutEffortRule.TryRoundSlider(10.6, out var high));
        Assert.Equal(10, high);

        Assert.True(WorkoutEffortRule.TryRoundSlider(0.2, out var low));
        Assert.Equal(1, low);

        Assert.False(WorkoutEffortRule.TryRoundSlider(double.NaN, out _));
        Assert.False(WorkoutEffortRule.TryRoundSlider(double.PositiveInfinity, out _));
    }

    [Theory]
    [InlineData("1", true, 1)]
    [InlineData("10", true, 10)]
    [InlineData(" 7 ", true, 7)]
    [InlineData("0", false, 0)]
    [InlineData("11", false, 0)]
    [InlineData("4.2", false, 0)]
    [InlineData("", false, 0)]
    public void Visible_Effort_Is_Only_A_Recorded_Whole_One_To_Ten(string text, bool expected, int expectedValue)
    {
        Assert.Equal(expected, WorkoutEffortRule.TryParseLogged(text, out var effort));
        if (expected)
            Assert.Equal(expectedValue, effort);
    }

    [Fact]
    public void Untouched_Neutral_Thumb_Is_Not_A_Rating()
    {
        Assert.True(WorkoutEffortRule.IsUntouchedNeutralThumb(5, hasRecordedEffort: false));
        Assert.False(WorkoutEffortRule.IsUntouchedNeutralThumb(5, hasRecordedEffort: true));
        Assert.False(WorkoutEffortRule.IsUntouchedNeutralThumb(4, hasRecordedEffort: false));
    }

    [Fact]
    public void Display_Text_Matches_The_Recorded_Effort()
    {
        Assert.Equal("4 / 10", WorkoutEffortRule.Format(4));
    }
}
