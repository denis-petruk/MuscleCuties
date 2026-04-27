using NSubstitute;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();
    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();

    private DashboardViewModel CreateViewModel() =>
        new(_authService, _cycleService, _nutritionService, _workoutRepository, () => { }, () => { }, () => { });

    [Fact]
    public async Task LoadData_SetsCurrentPhase_AndCaloriesData()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Follicular)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(800f);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(CyclePhase.Follicular, vm.CurrentPhase);
        Assert.Equal(2000f, vm.TargetCalories);
        Assert.Equal(800f, vm.ConsumedCalories);
        Assert.Equal("Follicular", vm.PhaseLabel);
    }

    [Fact]
    public async Task LoadData_CalculatesCaloriesProgress()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Luteal);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Luteal)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(2500f);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1f, vm.CaloriesProgress);
    }
}
