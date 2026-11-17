using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public class WorkoutActivityTagRuleTests
{
    [Theory]
    [InlineData("Walking Lunge")]
    [InlineData("Banded Lateral Walk")]
    [InlineData("Farmer's Walk")]
    [InlineData("Goblet Squat")]
    public void Loaded_walks_and_strength_movements_stay_in_the_strength_section(string name)
    {
        Assert.Equal(WorkoutActivityTagRule.Strength,
            WorkoutActivityTagRule.Classify("Strength", name, durationSeconds: null));
        Assert.False(WorkoutActivityTagRule.UsesEnduranceMetrics(name, durationSeconds: null));
    }

    [Fact]
    public void A_timed_walk_is_conditioning_work()
    {
        Assert.Equal(WorkoutActivityTagRule.Cardio,
            WorkoutActivityTagRule.Classify("Strength", "Walking Lunge", durationSeconds: 600));
        Assert.True(WorkoutActivityTagRule.UsesEnduranceMetrics("Banded Lateral Walk", durationSeconds: 600));
    }

    [Theory]
    [InlineData("Easy Walk")]
    [InlineData("Treadmill Walk")]
    public void A_walk_on_a_cardio_day_is_conditioning_work(string name)
    {
        Assert.Equal(WorkoutActivityTagRule.Cardio,
            WorkoutActivityTagRule.Classify("Cardio", name, durationSeconds: null));
    }

    [Theory]
    [InlineData("Rowing Intervals", "Strength")]
    [InlineData("Stationary Bike", "Strength")]
    [InlineData("Morning Run", "Strength")]
    [InlineData("Dance Cardio", "Cardio")]
    public void Inherently_aerobic_names_are_conditioning_work(string name, string workoutType)
    {
        Assert.Equal(WorkoutActivityTagRule.Cardio,
            WorkoutActivityTagRule.Classify(workoutType, name, durationSeconds: null));
        Assert.True(WorkoutActivityTagRule.UsesEnduranceMetrics(name, durationSeconds: null));
    }

    [Theory]
    [InlineData("Yoga Flow")]
    [InlineData("Hip Mobility Circuit")]
    [InlineData("Box Breathing")]
    public void Recovery_names_are_recovery_work(string name)
    {
        Assert.Equal(WorkoutActivityTagRule.Recovery,
            WorkoutActivityTagRule.Classify("Strength", name, durationSeconds: null));
    }

    [Fact]
    public void A_recovery_day_without_a_matching_name_is_recovery_work()
    {
        Assert.Equal(WorkoutActivityTagRule.Recovery,
            WorkoutActivityTagRule.Classify("Recovery", "Easy Circuit", durationSeconds: null));
    }

    [Fact]
    public void An_empty_name_on_a_strength_day_is_strength_work()
    {
        Assert.Equal(WorkoutActivityTagRule.Strength,
            WorkoutActivityTagRule.Classify("Strength", exerciseName: null, durationSeconds: null));
    }

    [Fact]
    public void Endurance_metrics_follow_the_prescription_duration()
    {
        Assert.True(WorkoutActivityTagRule.UsesEnduranceMetrics("Goblet Squat", durationSeconds: 45));
        Assert.True(WorkoutActivityTagRule.UsesEnduranceMetrics("Plank", durationSeconds: null));
        Assert.False(WorkoutActivityTagRule.UsesEnduranceMetrics("Walking Lunge", durationSeconds: 0));
    }
}
