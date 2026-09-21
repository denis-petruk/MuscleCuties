using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.ViewModels.Profile;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Profile;

public sealed class InjuryLogViewModelTests : IDisposable
{
    private readonly IWorkoutInjuryRepository _repository = Substitute.For<IWorkoutInjuryRepository>();
    private readonly IWorkoutService _workouts = Substitute.For<IWorkoutService>();
    private readonly IAppPreloadService _preload = Substitute.For<IAppPreloadService>();
    private readonly List<WorkoutInjuryLog> _logs = [];
    private readonly ServiceProvider _services;

    public InjuryLogViewModelTests()
    {
        var auth = Substitute.For<IAuthService>();
        var cycle = Substitute.For<ICycleService>();
        auth.GetCurrentUserIdAsync().Returns(7);
        cycle.GetCurrentPhaseAsync(7).Returns(CyclePhase.Follicular);
        _repository.GetAllAsync(7).Returns(_ => _logs.ToArray());
        _repository.SaveAsync(Arg.Any<WorkoutInjuryLog>()).Returns(call =>
        {
            var log = call.Arg<WorkoutInjuryLog>();
            if (log.Id == 0)
                log.Id = _logs.Count + 1;
            _logs.RemoveAll(item => item.Id == log.Id);
            _logs.Add(log);
            return Task.CompletedTask;
        });
        _repository.DeleteAsync(Arg.Any<int>()).Returns(call =>
        {
            _logs.RemoveAll(item => item.Id == call.Arg<int>());
            return Task.CompletedTask;
        });
        _services = new ServiceCollection()
            .AddSingleton(auth).AddSingleton(cycle).AddSingleton(_repository)
            .AddSingleton(_workouts).AddSingleton(_preload).BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private InjuryLogViewModel CreateViewModel(Func<Task>? changed = null) => new(
        _services.GetRequiredService<IServiceScopeFactory>(), () => Task.CompletedTask, changed);

    [Fact]
    public async Task EmptyState_AppearsOnlyAfterSuccessfulLoad()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasNoInjuries);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoInjuries);
        Assert.False(vm.IsBusy);
        vm.ToggleEducationCommand.Execute(null);
        Assert.True(vm.IsEducationExpanded);
    }

    [Fact]
    public async Task FailedLoad_ShowsRetryAndNeverClaimsAnEmptyLog()
    {
        _repository.GetAllAsync(7).Returns(Task.FromException<IReadOnlyList<WorkoutInjuryLog>>(
            new InvalidOperationException("Database unavailable")));
        var vm = CreateViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsLoadError);
        Assert.True(vm.HasError);
        Assert.False(vm.HasNoInjuries);
        Assert.False(vm.IsBusy);
        Assert.True(vm.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_RequiresBodyAreaAndValidDate()
    {
        var vm = CreateViewModel();
        Assert.False(vm.SaveInjuryCommand.CanExecute(null));
        await vm.SaveInjuryCommand.ExecuteAsync(null);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<WorkoutInjuryLog>());

        vm.SelectedSiteFlag = InjuryFlag.Knee;
        Assert.True(vm.SaveInjuryCommand.CanExecute(null));
        vm.SinceDateValue = DateTime.Today.AddDays(1);
        Assert.False(vm.SaveInjuryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_WaitsForPlanBeforeRefreshingParent()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = false;
        _workouts.RegenerateActivePlanAsync(7, CyclePhase.Follicular).Returns(_ =>
        {
            started.SetResult();
            return completion.Task;
        });
        var vm = CreateViewModel(() => { refreshed = true; return Task.CompletedTask; });
        vm.SelectedSiteFlag = InjuryFlag.Knee;

        var save = vm.SaveInjuryCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.IsBusy);
        Assert.False(refreshed);
        completion.SetResult();
        await save;

        Assert.True(refreshed);
        Assert.True(vm.HasActiveInjuries);
        Assert.False(vm.ShowAddForm);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        _preload.Received(1).InvalidateDashboard();
        _preload.Received(1).InvalidateWorkout();
    }

    [Fact]
    public async Task ClearThenDelete_UpdatesHistoryAndEmptyState()
    {
        var vm = CreateViewModel();
        vm.SelectedSiteFlag = InjuryFlag.Knee;
        await vm.SaveInjuryCommand.ExecuteAsync(null);
        vm.EditInjuryCommand.Execute(vm.ActiveInjuries.Single());
        vm.SelectedStatus = InjuryStatus.Cleared;

        await vm.SaveInjuryCommand.ExecuteAsync(null);

        Assert.False(vm.HasActiveInjuries);
        Assert.True(vm.HasClearedInjuries);
        Assert.False(vm.HasNoInjuries);
        await vm.DeleteInjuryCommand.ExecuteAsync(vm.ClearedInjuries.Single().Id);
        Assert.True(vm.HasNoInjuries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPlanRefresh_CanRetryWithoutSavingDuplicateInjury(bool reopenSheet)
    {
        _workouts.RegenerateActivePlanAsync(7, CyclePhase.Follicular)
            .Returns(Task.FromException(new InvalidOperationException("Plan unavailable")));
        var vm = CreateViewModel();
        vm.SelectedSiteFlag = InjuryFlag.Shoulder;

        await vm.SaveInjuryCommand.ExecuteAsync(null);

        Assert.True(vm.IsPlanRefreshError);
        Assert.True(vm.HasActiveInjuries);
        Assert.Contains("was saved", vm.ErrorText);
        _workouts.RegenerateActivePlanAsync(7, CyclePhase.Follicular).Returns(Task.CompletedTask);
        if (reopenSheet)
            await vm.LoadCommand.ExecuteAsync(null);
        else
            await vm.RetryPlanRefreshCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.False(vm.IsPlanRefreshError);
        Assert.Single(_logs);
        await _repository.Received(1).SaveAsync(Arg.Any<WorkoutInjuryLog>());
    }
}
