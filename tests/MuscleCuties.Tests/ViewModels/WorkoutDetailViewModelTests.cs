using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Tests.ViewModels;

public class WorkoutDetailViewModelTests
{
    private readonly IWorkoutRepository _repo = Substitute.For<IWorkoutRepository>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private bool _loggedCompletion;

    private WorkoutDetailViewModel CreateViewModel(int workoutDayId = 1) =>
        new(_repo, _authService, workoutDayId, () => _loggedCompletion = true);

    private static WorkoutDay MakeDay() => new WorkoutDay
    {
        Id = 1,
        Name = "Follicular Strength Day 1",
        WorkoutType = WorkoutType.Strength,
        DurationMinutes = 45,
        WorkoutDayExercises = new List<WorkoutDayExercise>
        {
            new WorkoutDayExercise
            {
                Sets = 3, Reps = 12,
                Exercise = new Exercise { Code = "GOBLET_SQUAT", Name = "Goblet Squat", Description = "A squat.", PrimaryMuscle = MuscleGroup.Quads }
            },
            new WorkoutDayExercise
            {
                Sets = 3, Reps = 10,
                Exercise = new Exercise { Code = "INCLINE_PUSH_UP_BENCH", Name = "Incline Push-Up on Bench", Description = "A push-up.", PrimaryMuscle = MuscleGroup.Chest }
            }
        }
    };

    [Fact]
    public async Task LoadData_PopulatesExercisesAndTitle()
    {
        _repo.GetWorkoutDayWithExercisesAsync(1).Returns(MakeDay());

        var vm = CreateViewModel(workoutDayId: 1);
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Follicular Strength Day 1", vm.WorkoutTitle);
        Assert.Equal(2, vm.Exercises.Count);
        Assert.Equal("Goblet Squat", vm.Exercises[0].Name);
        Assert.Equal("45 min", vm.DurationText);
        Assert.Equal("STRENGTH", vm.WorkoutTypeLabel);
    }

    [Fact]
    public async Task LoadData_DayNotFound_ExercisesEmpty()
    {
        _repo.GetWorkoutDayWithExercisesAsync(99).Returns((WorkoutDay?)null);

        var vm = CreateViewModel(workoutDayId: 99);
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Empty(vm.Exercises);
    }

    [Fact]
    public async Task LogCompletion_100Percent_CallsRepositoryAndNavigatesBack()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _repo.GetWorkoutDayWithExercisesAsync(1).Returns(MakeDay());

        var vm = CreateViewModel(workoutDayId: 1);
        await vm.LoadDataCommand.ExecuteAsync(null);

        await vm.LogCompletionCommand.ExecuteAsync(null);

        await _repo.Received(1).AddWorkoutLogAsync(Arg.Is<WorkoutLog>(l =>
            l.UserId == 1 && l.WorkoutDayId == 1 && l.CompletionPercent == 100));
        Assert.True(_loggedCompletion);
    }

    [Fact]
    public void ExerciseItem_WithDuration_ShowsDurationLabel()
    {
        var item = new ExerciseItem { Sets = 1, Reps = 0, DurationSeconds = 60 };
        Assert.Equal("1 sets · 60s", item.SetsRepsLabel);
    }

    [Fact]
    public void ExerciseItem_WithReps_ShowsRepsLabel()
    {
        var item = new ExerciseItem { Sets = 3, Reps = 12, DurationSeconds = null };
        Assert.Equal("3 sets × 12 reps", item.SetsRepsLabel);
    }
}
