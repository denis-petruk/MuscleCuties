using NSubstitute;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Tests.ViewModels;

public class NutritionViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();
    private readonly INutritionService _nutritionService = Substitute.For<INutritionService>();

    private NutritionViewModel CreateViewModel() =>
        new(_authService, _cycleService, _nutritionService);

    [Fact]
    public async Task LoadData_SetsTargetsAndConsumed()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Ovulatory);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Ovulatory)
            .Returns((1800f, 130f, 180f, 60f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(900f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((50f, 90f, 30f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1800f, vm.TargetCalories);
        Assert.Equal(130f, vm.TargetProtein);
        Assert.Equal(180f, vm.TargetCarbs);
        Assert.Equal(60f, vm.TargetFats);
        Assert.Equal(900f, vm.ConsumedCalories);
        Assert.Equal(50f, vm.ConsumedProtein);
        Assert.Equal(90f, vm.ConsumedCarbs);
        Assert.Equal(30f, vm.ConsumedFats);
    }

    [Fact]
    public async Task LoadData_CaloriesProgressClampedToOne()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Menstrual);
        _nutritionService.CalculateDailyTargetsAsync(1, CyclePhase.Menstrual)
            .Returns((2000f, 150f, 200f, 70f));
        _nutritionService.GetConsumedCaloriesAsync(1, Arg.Any<DateTime>()).Returns(3000f);
        _nutritionService.GetConsumedMacrosAsync(1, Arg.Any<DateTime>()).Returns((0f, 0f, 0f));

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1f, vm.CaloriesProgress);
    }
}
