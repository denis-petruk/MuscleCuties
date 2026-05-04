using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Tests.ViewModels;

public class ProfileViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();
    private readonly ICycleRepository _cycleRepository = Substitute.For<ICycleRepository>();
    private bool _navigatedToLogin;

    private ProfileViewModel CreateViewModel() =>
        new(_authService, _userRepository, _workoutRepository, _cycleRepository, () => _navigatedToLogin = true);

    [Fact]
    public async Task LoadData_SetsNameEmailGoal()
    {
        var user = new User { Id = 1, Email = "user@test.com", PasswordHash = "hash" };
        var profile = new UserProfile
        {
            Id = 1,
            UserId = 1,
            Name = "Jane",
            Goal = UserGoal.FatLoss
        };

        _authService.GetCurrentUserIdAsync().Returns(1);
        _userRepository.GetByIdAsync(1).Returns(user);
        _userRepository.GetProfileAsync(1).Returns(profile);

        var vm = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal("Jane", vm.Name);
        Assert.Equal("user@test.com", vm.Email);
        Assert.Equal(UserGoal.FatLoss, vm.Goal);
    }

    [Fact]
    public async Task Logout_CallsLogoutAsync_AndNavigates()
    {
        var vm = CreateViewModel();
        await vm.LogoutCommand.ExecuteAsync(null);

        await _authService.Received(1).LogoutAsync();
        Assert.True(_navigatedToLogin);
    }
}
