using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout;

public class WorkoutPlannerTests
{
    private readonly WorkoutPlanner _planner = new();

    [Fact]
    public void BuildWorkoutItems_UsesActivityTagAndExerciseSummary()
    {
        var day = new WorkoutDay
        {
            Id = 1,
            DayOfWeek = 1,
            WorkoutType = WorkoutType.Strength,
            Name = "Lower Body",
            WorkoutDayExercises =
            [
                new WorkoutDayExercise { Exercise = new Exercise { Name = "Squat" }, DurationSeconds = 600 },
                new WorkoutDayExercise { Exercise = new Exercise { Name = "Hip Thrust" }, DurationSeconds = 600 }
            ]
        };

        var workouts = _planner.BuildWorkoutItems([day]);
        var item = workouts.Single(workout => workout.DayLabel == "MON");

        Assert.Equal("STRENGTH", item.Tag);
        Assert.Equal("Lower Body", item.Title);
        Assert.Equal("20 min", item.Duration);
        Assert.Equal("2 exercises", item.ExerciseCountText);
        Assert.Equal("Squat and Hip Thrust", item.DetailsText);
        Assert.True(workouts.Single(workout => workout.DayLabel == "TUE").IsRestDay);
    }

    [Fact]
    public void BuildWorkoutItems_KeepsSeparateSessionsOnTheSameDay()
    {
        var sessions = new[]
        {
            new WorkoutDay { Id = 1, DayOfWeek = 2, WorkoutType = WorkoutType.Strength, Name = "Upper body strength" },
            new WorkoutDay { Id = 2, DayOfWeek = 2, WorkoutType = WorkoutType.Cardio, Name = "Running conditioning" }
        };

        var items = _planner.BuildWorkoutItems(sessions)
            .Where(workout => workout.DayLabel == "TUE")
            .ToList();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Tag == "STRENGTH");
        Assert.Contains(items, item => item.Tag == "CARDIO");
    }

    [Fact]
    public void BuildWorkoutItems_UsesLatestWorkoutLogForCompletionState()
    {
        var day = new WorkoutDay
        {
            Id = 21,
            DayOfWeek = 1,
            WorkoutType = WorkoutType.Strength,
            Name = "Upper Body"
        };

        var item = _planner.BuildWorkoutItems(
                [day],
                [new WorkoutLog { WorkoutDayId = 21, Date = DateTime.Today, CompletionPercent = 100 }])
            .Single(workout => workout.DayLabel == "MON");

        Assert.True(item.IsCompleted);
        Assert.Equal("Completed", item.SessionProgressText);
    }

    [Fact]
    public void BuildTodaysSummary_CompletedOvulatoryWorkoutShowsHighIntensity()
    {
        var plan = new WorkoutPlan { Id = 7, Name = "Cycle Strength" };
        var day = new WorkoutDay
        {
            Id = 12,
            WorkoutPlanId = 7,
            DayOfWeek = 3,
            Name = "Lower Body",
            WorkoutDayExercises =
            [
                new WorkoutDayExercise(),
                new WorkoutDayExercise(),
                new WorkoutDayExercise(),
                new WorkoutDayExercise()
            ]
        };

        var summary = _planner.BuildTodaysSummary(
            plan,
            [day],
            [new WorkoutLog { WorkoutDayId = 12, CompletionPercent = 100 }],
            CyclePhase.Ovulatory,
            new DateTime(2026, 8, 12));

        Assert.Equal("Lower Body", summary.Title);
        Assert.Equal("45 min", summary.DurationText);
        Assert.Equal("4", summary.ExercisesCount);
        Assert.Equal("High", summary.Intensity);
        Assert.Equal("Completed", summary.SessionProgressText);
    }
}
