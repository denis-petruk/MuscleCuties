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
        Assert.Equal("1 activity", item.ActivityCountText);
        Assert.Equal("Squat and Hip Thrust", item.DetailsText);

        var restDay = _planner.BuildWorkoutItems([day]).Single(workout => workout.DayLabel == "TUE");
        Assert.Equal("REST", restDay.Tag);
        Assert.Equal("Living happy life", restDay.Title);
        Assert.True(restDay.IsRestDay);
    }

    [Fact]
    public void BuildWorkoutItems_UsesRecoveryGroupWhenSessionIncludesYoga()
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

        Assert.Equal("RECOVERY", item.Tag);
        Assert.Equal("TUE", item.DayLabel);
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

        var item = _planner
            .BuildWorkoutItems(
                [day],
                [
                    new WorkoutLog
                    {
                        WorkoutDayId = 21,
                        Date = DateTime.Today,
                        CompletionPercent = 100,
                        CreatedAt = DateTime.UtcNow
                    }
                ])
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

        Assert.Equal("Progressive strength training", plan.Name);
        Assert.Equal(CyclePhase.Follicular, plan.CyclePhaseTarget);
        Assert.True(plan.IsActive);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], plan.WorkoutDays.Select(day => day.DayOfWeek));
        Assert.Equal(4, plan.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.All(plan.WorkoutDays.Where(day => day.WorkoutType != WorkoutType.Rest), day => Assert.NotEmpty(day.WorkoutDayExercises));
        Assert.Equal(2, plan.WorkoutDays.Count(day => day.WorkoutType == WorkoutType.Strength));
        Assert.DoesNotContain(plan.WorkoutDays, day => day.WorkoutType == WorkoutType.Cardio);
        Assert.Contains(plan.WorkoutDays, day => day.WorkoutType == WorkoutType.Recovery);
        Assert.All(plan.WorkoutDays.Where(day => day.WorkoutType == WorkoutType.Rest), day => Assert.Empty(day.WorkoutDayExercises));
        Assert.Contains(plan.WorkoutDays.SelectMany(day => day.WorkoutDayExercises), exercise =>
            exercise.Sets == 4 && exercise.Reps == 5);
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Lower body strength");

        var lowerBodyItem = _planner
            .BuildWorkoutItems(plan.WorkoutDays.ToList())
            .Single(workout => workout.Title == "Lower body strength");
        Assert.Equal("92 min", lowerBodyItem.Duration);
    }

    [Fact]
    public void BuildGeneratedPlan_StrengthGoalKeepsAtLeastTwoTrainingDaysWhenNotDeloading()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.Strength,
            TrainingExperienceLevel = TrainingExperienceLevel.Beginner,
            WorkoutDaysPerWeek = 1
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.Equal(2, plan.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
    }

    [Fact]
    public void BuildGeneratedPlan_FatLossProfileMarksTimedSessionsAsCardio()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.FatLoss,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 4,
            PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
            [
                WorkoutActivityType.HighVolumeStrength,
                WorkoutActivityType.Hiit,
                WorkoutActivityType.Yoga
            ])
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.Contains(plan.WorkoutDays, day =>
            day.Name == "HIIT conditioning" && day.WorkoutType == WorkoutType.Cardio);
    }

    [Fact]
    public void BuildGeneratedPlan_NoCardioPreferenceUsesRecoveryInstead()
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

        Assert.Equal("Strength-based fat loss training", plan.Name);
        Assert.DoesNotContain(plan.WorkoutDays, day => day.WorkoutType == WorkoutType.Cardio);
        Assert.Contains(plan.WorkoutDays, day => day.WorkoutType == WorkoutType.Recovery);
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
                WorkoutActivityType.Yoga
            ])
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        Assert.Contains(plan.WorkoutDays, day => day.Name == "Climbing pull strength");
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Climbing pull strength" && day.WorkoutType == WorkoutType.Strength);
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Low intensity recovery training" && day.WorkoutType == WorkoutType.Recovery);
        Assert.Contains(plan.WorkoutDays, day =>
            day.WorkoutDayExercises.Any(exercise => exercise.DurationSeconds > 0));
        Assert.Contains(plan.WorkoutDays.SelectMany(day => day.WorkoutDayExercises), exercise =>
            exercise.ExerciseId > 0 && exercise.DurationSeconds >= 3_000);
    }

    [Fact]
    public void BuildGeneratedPlan_AdvancedPhysiqueProfileUsesRicherMovementLibrary()
    {
        var exerciseLibrary = BuildExerciseLibrary();
        var namesById = exerciseLibrary.ToDictionary(exercise => exercise.Id, exercise => exercise.Name);
        var profile = new UserProfile
        {
            Goal = UserGoal.MuscleTone,
            TrainingExperienceLevel = TrainingExperienceLevel.Advanced,
            WorkoutDaysPerWeek = 5
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            exerciseLibrary,
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        var plannedExerciseNames = plan.WorkoutDays
            .SelectMany(day => day.WorkoutDayExercises)
            .Select(exercise => namesById[exercise.ExerciseId])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Advanced physique strength", plan.Name);
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Advanced lower body strength");
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Glutes and hamstrings");
        Assert.Contains(plan.WorkoutDays, day => day.Name == "Upper pull and shoulders");
        Assert.Contains("Barbell Hip Thrust", plannedExerciseNames);
        Assert.Contains("Bulgarian Split Squat", plannedExerciseNames);
        Assert.Contains("Cable Glute Kickback", plannedExerciseNames);
        Assert.Contains("Cable Hip Abduction", plannedExerciseNames);
        Assert.Contains("Assisted Pull-Up", plannedExerciseNames);
        Assert.Contains("Rear Delt Fly", plannedExerciseNames);
        Assert.Contains("Cable Woodchop", plannedExerciseNames);
        Assert.All(
            plan.WorkoutDays.Where(day => day.WorkoutType == WorkoutType.Strength),
            day => Assert.True(day.WorkoutDayExercises.Count >= 7));
    }

    [Fact]
    public void BuildGeneratedPlan_YogaPreferenceCreatesFullLengthRecoverySession()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.MaintainHealth,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 3,
            PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
            [
                WorkoutActivityType.Yoga
            ])
        };

        var plan = _planner.BuildGeneratedPlan(
            9,
            profile,
            null,
            BuildExerciseLibrary(),
            CyclePhase.Follicular,
            new DateTime(2026, 8, 17));

        var yogaDay = plan.WorkoutDays.Single(day => day.Name == "Low intensity recovery training");
        var yogaSeconds = yogaDay.WorkoutDayExercises.Sum(exercise => exercise.DurationSeconds ?? 0);

        Assert.Equal(WorkoutType.Recovery, yogaDay.WorkoutType);
        Assert.InRange(yogaSeconds, 3_000, 5_400);
    }

    [Fact]
    public void BuildGeneratedPlan_WithManyPreferencesUsesPhaseAppropriateSubset()
    {
        var profile = new UserProfile
        {
            Goal = UserGoal.MaintainHealth,
            TrainingExperienceLevel = TrainingExperienceLevel.Intermediate,
            WorkoutDaysPerWeek = 3,
            PreferredWorkoutActivityTypes = WorkoutActivityPreferences.Serialize(
            [
                WorkoutActivityType.HighVolumeStrength,
                WorkoutActivityType.RockClimbing,
                WorkoutActivityType.Hiit,
                WorkoutActivityType.Yoga
            ])
        };
        var snapshot = new UserProfileSnapshot
        {
            ProfileJson = """
            {
              "CyclePhaseBaselines": {
                "Menstrual": { "Pain": 3, "Energy": 2 }
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

        var activeDays = plan.WorkoutDays
            .Where(day => day.WorkoutType != WorkoutType.Rest)
            .ToList();

        Assert.Equal(2, activeDays.Count);
        Assert.DoesNotContain(activeDays, day => day.Name is "Climbing pull strength" or "Conditioning intervals");
        Assert.Contains(activeDays, day => day.WorkoutType == WorkoutType.Recovery);
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

        Assert.Equal("Low intensity recovery training", plan.Name);
        Assert.Equal(7, plan.WorkoutDays.Count);
        Assert.Equal(4, plan.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.Contains(plan.WorkoutDays.SelectMany(day => day.WorkoutDayExercises), exercise =>
            exercise.Sets == 2 && exercise.Reps == 10);
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
            "Barbell Hip Thrust",
            "Romanian Deadlift",
            "Single-Leg Romanian Deadlift",
            "Cable Pull-Through",
            "Step-Up",
            "Reverse Lunge",
            "Bulgarian Split Squat",
            "Walking Lunge",
            "Leg Press",
            "Leg Extension",
            "Seated Leg Curl",
            "Glute Bridge",
            "Cable Glute Kickback",
            "Cable Hip Abduction",
            "Back Extension",
            "Calf Raise",
            "Incline Push-Up",
            "Incline Dumbbell Press",
            "Dumbbell Row",
            "Chest Supported Row",
            "Single-Arm Cable Row",
            "Seated Cable Row",
            "Lat Pulldown",
            "Assisted Pull-Up",
            "Overhead Press",
            "Lateral Raise",
            "Cable Lateral Raise",
            "Rear Delt Fly",
            "Face Pull",
            "Biceps Curl",
            "Triceps Pressdown",
            "Dead Bug",
            "Side Plank",
            "Copenhagen Side Plank",
            "Pallof Press",
            "Cable Woodchop",
            "Bird Dog",
            "Plank",
            "Hanging Knee Raise",
            "Reverse Crunch",
            "Bike Intervals",
            "HIIT Intervals",
            "Cycling Intervals",
            "Running Intervals",
            "Tempo Run",
            "Easy Run",
            "Zone 2 Ride",
            "Easy Walk",
            "Mobility Flow",
            "Yoga Flow",
            "Slow Flow Yoga",
            "Hip Opening Yoga",
            "Vinyasa Flow",
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
