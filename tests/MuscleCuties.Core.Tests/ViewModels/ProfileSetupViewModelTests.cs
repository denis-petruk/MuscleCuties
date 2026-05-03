using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class ProfileSetupViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private bool _navigatedToDashboard;

    private ProfileSetupViewModel CreateViewModel() =>
        new(_authService, _userRepository, () => _navigatedToDashboard = true);

    [Fact]
    public async Task Save_ValidData_CallsAddProfileAsync_AndNavigates()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(Arg.Any<int>()).Returns((UserProfile?)null);

        var vm = CreateViewModel();
        vm.Name = "Jane";
        vm.Height = 165f;
        vm.Weight = 60f;
        vm.WorkoutDaysPerWeek = 4;
        vm.CycleLength = 28;

        await vm.SaveCommand.ExecuteAsync(null);

        await _userRepository.Received(1).AddProfileAsync(Arg.Is<UserProfile>(p =>
            p.UserId == 1 &&
            p.Name == "Jane"));
        Assert.True(_navigatedToDashboard);
    }

    [Fact]
    public async Task Save_WhenCalled_SetsBusy()
    {
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(Arg.Any<int>()).Returns((UserProfile?)null);

        var vm = CreateViewModel();
        vm.Name = "Jane";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Save_WhenProfileAlreadyExists_CallsUpdateProfileAsync_NotAdd()
    {
        var existing = new UserProfile { Id = 1, UserId = 1 };
        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetProfileAsync(Arg.Any<int>()).Returns(existing);

        var vm = CreateViewModel();
        vm.Name = "Jane";
        vm.Height = 165f;
        vm.Weight = 60f;
        vm.WorkoutDaysPerWeek = 4;
        vm.CycleLength = 28;

        await vm.ContinueCommand.ExecuteAsync(null);

        await _userRepository.Received(1).UpdateProfileAsync(Arg.Any<UserProfile>());
        await _userRepository.DidNotReceive().AddProfileAsync(Arg.Any<UserProfile>());
    }
}
