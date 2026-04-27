using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class CycleViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly ICycleService _cycleService = Substitute.For<ICycleService>();

    private CycleViewModel CreateViewModel() =>
        new(_authService, _cycleService);

    [Fact]
    public async Task LoadData_WithActiveCycle_SetsCycleDay()
    {
        var startDate = DateTime.Today.AddDays(-10);
        var cycle = new CycleLog
        {
            Id = 1,
            UserId = 1,
            CycleStartDate = startDate,
            CycleLength = 28
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentCycleAsync(1).Returns(cycle);
        _cycleService.CalculateCycleDay(startDate).Returns(11);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Follicular);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(11, vm.CycleDay);
        Assert.Equal(28, vm.CycleLength);
        Assert.Equal(CyclePhase.Follicular, vm.CurrentPhase);
    }

    [Fact]
    public async Task LoadData_WithNoCycle_CycleDayIsZero()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _cycleService.GetCurrentCycleAsync(1).Returns((CycleLog?)null);
        _cycleService.GetCurrentPhaseAsync(1).Returns(CyclePhase.Menstrual);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.CycleDay);
        Assert.Equal(0, vm.CycleLength);
    }
}
