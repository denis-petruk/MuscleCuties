using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class WorkoutViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();

    private WorkoutViewModel CreateViewModel() =>
        new(_authService, _cycleService, _workoutRepository);

    [Fact]
    public async Task LoadData_WithActivePlan_LoadsWorkoutDays()
    {
        var plan = new WorkoutPlan { Id = 10, UserId = 1, Name = "Full Body", DaysPerWeek = 3, Phase = CyclePhase.Follicular };
        var days = new List<WorkoutDay>
        {
            new WorkoutDay { Id = 1, WorkoutPlanId = 10, DayNumber = 1, Name = "Day 1" },
            new WorkoutDay { Id = 2, WorkoutPlanId = 10, DayNumber = 2, Name = "Day 2" }
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutRepository.GetActivePlanAsync(1).Returns(plan);
        _workoutRepository.GetWorkoutDaysByPlanAsync(10).Returns(days);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(plan, vm.ActivePlan);
        Assert.Equal(2, vm.WorkoutDays.Count);
    }

    [Fact]
    public async Task LoadData_WithNoActivePlan_ActivePlanIsNull()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _workoutRepository.GetActivePlanAsync(1).Returns((WorkoutPlan?)null);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Null(vm.ActivePlan);
        Assert.Empty(vm.WorkoutDays);
    }
}
