using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.Tests.Services;

public class WorkoutServiceTests
{
    private readonly IWorkoutRepository _workoutRepo = Substitute.For<IWorkoutRepository>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();

    private WorkoutService CreateService() => new(_workoutRepo, _cycleService);

    private void SetupEmptyExercises()
    {
        _workoutRepo.GetExercisesByCodesAsync(Arg.Any<IEnumerable<string>>()).Returns(new List<Exercise>());
    }

    [Fact]
    public async Task GenerateUserPlans_StrengthGoal_3Days_Creates4Plans()
    {
        SetupEmptyExercises();
        _workoutRepo.GetAllUserPlansAsync(1).Returns(new List<WorkoutPlan>());
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);

        var svc = CreateService();
        await svc.GenerateUserPlansAsync(1, UserGoal.Strength, 3);

        await _workoutRepo.Received(4).AddAsync(Arg.Any<WorkoutPlan>());
    }

    [Fact]
    public async Task GenerateUserPlans_MenstrualPhase_AllDaysAreRecovery()
    {
        SetupEmptyExercises();
        _workoutRepo.GetAllUserPlansAsync(1).Returns(new List<WorkoutPlan>());
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);

        var capturedPlans = new List<WorkoutPlan>();
        await _workoutRepo.AddAsync(Arg.Do<WorkoutPlan>(p => capturedPlans.Add(p)));

        var svc = CreateService();
        await svc.GenerateUserPlansAsync(1, UserGoal.Strength, 3);

        var menstrualPlan = capturedPlans.First(p => p.CyclePhaseTarget == CyclePhase.Menstrual);
        Assert.True(menstrualPlan.WorkoutDays.All(d => d.WorkoutType == WorkoutType.Recovery));
    }

    [Fact]
    public async Task GenerateUserPlans_FatLossGoal_FollicularPhase_HasCardio()
    {
        SetupEmptyExercises();
        _workoutRepo.GetAllUserPlansAsync(1).Returns(new List<WorkoutPlan>());
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);

        var capturedPlans = new List<WorkoutPlan>();
        await _workoutRepo.AddAsync(Arg.Do<WorkoutPlan>(p => capturedPlans.Add(p)));

        var svc = CreateService();
        await svc.GenerateUserPlansAsync(1, UserGoal.FatLoss, 3);

        var follicularPlan = capturedPlans.First(p => p.CyclePhaseTarget == CyclePhase.Follicular);
        Assert.Contains(follicularPlan.WorkoutDays, d => d.WorkoutType == WorkoutType.Cardio);
    }

    [Fact]
    public async Task GetTodaysWorkout_ActivePlanExists_ReturnsDay()
    {
        var plan = new WorkoutPlan { Id = 1, UserId = 1, Name = "Test", IsActive = true };
        var todayDow = (int)DateTime.Today.DayOfWeek;
        plan.WorkoutDays.Add(new WorkoutDay { Id = 10, WorkoutPlanId = 1, DayOfWeek = todayDow, Name = "Today", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 });
        _workoutRepo.GetActivePlanAsync(1).Returns(plan);

        var svc = CreateService();
        var day = await svc.GetTodaysWorkoutAsync(1);

        Assert.NotNull(day);
        Assert.Equal(10, day!.Id);
    }

    [Fact]
    public async Task GetTodaysWorkout_NoPlan_ReturnsNull()
    {
        _workoutRepo.GetActivePlanAsync(1).Returns((WorkoutPlan?)null);

        var svc = CreateService();
        var day = await svc.GetTodaysWorkoutAsync(1);

        Assert.Null(day);
    }

    [Fact]
    public async Task GetTodaysWorkout_NoWorkoutScheduledToday_ReturnsNull()
    {
        var plan = new WorkoutPlan { Id = 1, UserId = 1, Name = "Test", IsActive = true };
        var todayDow = (int)DateTime.Today.DayOfWeek;
        var notTodayDow = (todayDow + 1) % 7;
        plan.WorkoutDays.Add(new WorkoutDay { Id = 10, WorkoutPlanId = 1, DayOfWeek = notTodayDow, Name = "Not Today", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 });
        _workoutRepo.GetActivePlanAsync(1).Returns(plan);

        var svc = CreateService();
        var day = await svc.GetTodaysWorkoutAsync(1);

        Assert.Null(day);
    }

    [Fact]
    public async Task SyncActivePlanToPhase_ActivatesMatchingPlan()
    {
        _workoutRepo.GetPlanByPhaseAsync(1, CyclePhase.Follicular)
            .Returns(new WorkoutPlan { Id = 5, UserId = 1, Name = "Follicular", CyclePhaseTarget = CyclePhase.Follicular, IsActive = false });

        var svc = CreateService();
        await svc.SyncActivePlanToPhaseAsync(1, CyclePhase.Follicular);

        await _workoutRepo.Received(1).DeactivateAllUserPlansAsync(1);
        await _workoutRepo.Received(1).UpdateAsync(Arg.Is<WorkoutPlan>(p => p.Id == 5 && p.IsActive));
    }
}
