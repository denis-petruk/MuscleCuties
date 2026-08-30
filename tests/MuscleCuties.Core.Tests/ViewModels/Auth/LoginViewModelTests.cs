using NSubstitute;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Auth;

public class LoginViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private bool _navigatedToDashboard;
    private bool _navigatedToProfileSetup;

    private LoginViewModel CreateViewModel() =>
        new(
            _authService,
            () => _navigatedToDashboard = true,
            () => _navigatedToProfileSetup = true,
            () => { });

    [Fact]
    public async Task LoginAsync_ValidCredentials_OnboardingComplete_NavigatesToDashboard()
    {
        var user = new User { Id = 1, Email = "test@test.com", PasswordHash = "hash", IsOnboardingComplete = true };
        _authService.LoginAsync("test@test.com", "pass").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(_navigatedToDashboard);
        Assert.False(_navigatedToProfileSetup);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_OnboardingIncomplete_NavigatesToProfileSetup()
    {
        var user = new User { Id = 2, Email = "test@test.com", PasswordHash = "hash" };
        _authService.LoginAsync("test@test.com", "pass").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(_navigatedToDashboard);
        Assert.True(_navigatedToProfileSetup);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_SetsErrorMessage()
    {
        _authService.LoginAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((User?)null);

        var vm = CreateViewModel();
        vm.Email = "bad@test.com";
        vm.Password = "wrong";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Invalid email or password", vm.ErrorMessage);
        Assert.False(_navigatedToDashboard);
        Assert.False(_navigatedToProfileSetup);
    }

    [Fact]
    public async Task LoginAsync_SetsAndClearsBusy()
    {
        var user = new User { Id = 1, Email = "test@test.com", PasswordHash = "hash", IsOnboardingComplete = true };
        _authService.LoginAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(user);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoginAsync_ManualTracking_OnboardingComplete_NavigatesToDashboard()
    {
        var user = new User { Id = 3, Email = "manual@test.com", PasswordHash = "hash", IsOnboardingComplete = true };
        _authService.LoginAsync("manual@test.com", "pass").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "manual@test.com";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(_navigatedToDashboard);
        Assert.False(_navigatedToProfileSetup);
    }
}
