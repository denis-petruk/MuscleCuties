using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutExerciseEffortSliderTests
{
    [Fact]
    public void Missing_Effort_Remains_Unset_Until_Slider_Changes()
    {
        var exercise = new WorkoutExerciseItem();

        Assert.Equal(5, exercise.EffortSliderValue);
        Assert.Equal("Move to choose", exercise.EffortDisplayText);

        exercise.EffortSliderValue = 5;

        Assert.Equal(string.Empty, exercise.LoggedEffortText);
        exercise.CommitEffortSliderValue(5);
        Assert.Equal("5", exercise.LoggedEffortText);
        exercise.LoggedEffortText = string.Empty;
        exercise.EffortSliderValue = 4.2;
        Assert.Equal("4", exercise.LoggedEffortText);
        Assert.Equal("4 / 10", exercise.EffortDisplayText);
    }

    [Fact]
    public void Slider_Only_Produces_Whole_In_Range_Effort()
    {
        var exercise = new WorkoutExerciseItem { LoggedEffortText = "10" };

        Assert.Equal(10, exercise.EffortSliderValue);
        exercise.EffortSliderValue = -4;
        Assert.Equal("1", exercise.LoggedEffortText);
        exercise.EffortSliderValue = 100;
        Assert.Equal("10", exercise.LoggedEffortText);
        exercise.EffortSliderValue = double.NaN;
        Assert.Equal("10", exercise.LoggedEffortText);
    }
}
