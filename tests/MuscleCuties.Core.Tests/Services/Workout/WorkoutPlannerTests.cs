using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
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

        var item = _planner.BuildWorkoutItems([day]).Single(workout => workout.DayLabel == "MON");

        Assert.Equal("STRENGTH", item.Tag);
        Assert.Equal("MON", item.DayLabel);
        Assert.Equal("Lower Body", item.Title);
        Assert.Equal("20 min", item.Duration);
        Assert.Equal("2 exercises", item.ExerciseCountText);
        Assert.Equal("Squat and Hip Thrust", item.DetailsText);

        var restDay = _planner.BuildWorkoutItems([day]).Single(workout => workout.DayLabel == "TUE");
        Assert.Equal("REST", restDay.Tag);
        Assert.Equal("Living happy life", restDay.Title);
        Assert.True(restDay.IsRestDay);
    }

    [Fact]
    public void BuildWorkoutItems_UsesYogaActivityWhenSessionIncludesYoga()
    {
        var day = new WorkoutDay
        {
            Id = 1,
            DayOfWeek = 2,
            WorkoutType = WorkoutType.Recovery,
            Name = "Recovery flow",
            WorkoutDayExercises =
            [
                new WorkoutDayExercise { Exercise = new Exercise { Name = "Yoga Flow" }, DurationSeconds = 600 }
            ]
        };

        var item = _planner.BuildWorkoutItems([day]).Single(workout => workout.DayLabel == "TUE");

        Assert.Equal("YOGA", item.Tag);
        Assert.Equal("TUE", item.DayLabel);
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
        Assert.Equal("32 min", summary.DurationText);
        Assert.Equal("4", summary.ExercisesCount);
        Assert.Equal("High", summary.Intensity);
        Assert.Equal("COMPLETED", summary.SessionProgressText);
    }

    [Fact]
    public void BuildGeneratedPlan_StrengthProfileCreatesScheduledWorkoutDays()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.Strength,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 4
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.StartsWith("Glow Plan Power Bloom Four Days", plan.Name);
        Assert.Equal(CyclePhase.Follicular, plan.CyclePhaseTarget);
        Assert.True(plan.IsActive);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], plan.WorkoutDays.Select(day => day.DayOfWeek));
        Assert.Equal(4, plan.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.All(plan.WorkoutDays.Where(day => day.WorkoutType != WorkoutType.Rest), day => Assert.NotEmpty(day.WorkoutDayExercises));
        Assert.All(plan.WorkoutDays.Where(day => day.WorkoutType != WorkoutType.Rest), day => Assert.Equal(WorkoutType.Strength, day.WorkoutType));
        Assert.All(plan.WorkoutDays.Where(day => day.WorkoutType == WorkoutType.Rest), day => Assert.Empty(day.WorkoutDayExercises));
        Assert.Contains(plan.WorkoutDays.SelectMany(day => day.WorkoutDayExercises), exercise =>
            exercise.Sets == 3 && exercise.Reps == 6);
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Leg Day Glow");
    }

    [Fact]
    public void BuildGeneratedPlan_FatLossProfileMarksTimedSessionsAsCardio()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.FatLoss,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 4
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.Contains(plan.WorkoutDays, day =>
            day.Name == "Sweat Spark" && day.WorkoutType == WorkoutType.Cardio);
    }

    [Fact]
    public void BuildGeneratedPlan_WithActivityPreferencesCreatesMatchingSessions()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.MaintainHealth,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 3,
            PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
            [
                WorkoutActivityType.RockClimbing,
                WorkoutActivityType.ActiveRecovery,
                WorkoutActivityType.YinYoga
            ])
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.Contains(plan.WorkoutDays, day => day.Name == "Climb Strong");
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Easy Reset Flow" && day.WorkoutType == WorkoutType.Recovery);
        Assert.Contains(plan.WorkoutDays, day =>
            day.WorkoutDayExercises.Any(exercise => exercise.DurationSeconds > 0));
    }

    [Fact]
    public void BuildGeneratedPlan_HighPainMenstrualSnapshotDeloadsTrainingDays()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.MuscleTone,
            TrainingExperienceLevel = TrainingExperienceLevel.Advanced,
            WorkoutDaysPerWeek = 5
        };
        var snapshot = new UserProfileSnapshot
        {
            ProfileJson = """
            {
              "CyclePhaseBaselines": {
                "Menstrual": { "Pain": 5, "Energy": 1 }
              }
            }
            """
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            snapshot,
            BuildExerciseLibrary(),
            CyclePhase.Menstrual,
            new DateTime(2026, 8, 17));

        Assert.StartsWith("Glow Plan Tone Glow Four Days", plan.Name);
        Assert.Equal(7, plan.WorkoutDays.Count);
        Assert.Equal(4, plan.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.Contains(plan.WorkoutDays.SelectMany(day => day.WorkoutDayExercises), exercise =>
            exercise.Sets == 3 && exercise.Reps == 10);
    }

    [Fact]
    public void ShouldReplaceGeneratedPlan_CustomPlan_ReturnsFalse()
    {
        var plan = new WorkoutPlan { Name = "My own plan", CyclePhaseTarget = CyclePhase.Follicular };
        var profile = new UserProfile
        {
            Goal = UserGoal.MaintainHealth,
            TrainingExperienceLevel = TrainingExperienceLevel.Beginner,
            WorkoutDaysPerWeek = 3
        };

        var shouldReplace = _planner.ShouldReplaceGeneratedPlan(
            plan,
            [],
            profile,
            null,
            CyclePhase.Follicular);

        Assert.False(shouldReplace);
    }

    private static List<Exercise> BuildExerciseLibrary()
    {
        var names = new[]
        {
            "Goblet Squat",
            "Hip Thrust",
            "Romanian Deadlift",
            "Step-Up",
            "Reverse Lunge",
            "Glute Bridge",
            "Calf Raise",
            "Incline Push-Up",
            "Incline Dumbbell Press",
            "Dumbbell Row",
            "Seated Cable Row",
            "Lat Pulldown",
            "Overhead Press",
            "Lateral Raise",
            "Face Pull",
            "Biceps Curl",
            "Triceps Pressdown",
            "Dead Bug",
            "Side Plank",
            "Pallof Press",
            "Bird Dog",
            "Plank",
            "Bike Intervals",
            "Zone 2 Ride",
            "Easy Walk",
            "Mobility Flow",
            "Yoga Flow",
            "Power Yoga",
            "Yin Yoga",
            "Restorative Yoga",
            "Pilates Flow",
            "Active Recovery Flow",
            "Breathing Reset",
            "Rock Climbing",
            "Swimming",
            "Dance Cardio"
        };

        return names
            .Select((name, index) => new Exercise
            {
                Id = index + 1,
                Name = name,
                Description = name
            })
            .ToList();
    }
}
