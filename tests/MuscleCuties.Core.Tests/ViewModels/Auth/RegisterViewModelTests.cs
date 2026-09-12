using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.ViewModels.Auth;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Auth;

public class RegisterViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private bool _navigatedToProfileSetup;

    private RegisterViewModel CreateViewModel()
    {
        return new RegisterViewModel(
            _authService,
            () =>
            {
                _navigatedToProfileSetup = true;
                return Task.CompletedTask;
            },
            () => Task.CompletedTask);
    }

    [Fact]
    public async Task RegisterAsync_ValidCredentials_NavigatesToProfileSetup()
    {
        var user = new User { Id = 1, Email = "new@test.com", PasswordHash = "hash" };
        _authService.RegisterAsync("new@test.com", "Pass123!").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "Pass123!";
        vm.ConfirmPassword = "Pass123!";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(_navigatedToProfileSetup);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_InvalidEmail_SetsErrorMessage()
    {
        var vm = CreateViewModel();
        vm.Email = "not-an-email";
        vm.Password = "Pass123!";
        vm.ConfirmPassword = "Pass123!";

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Enter a valid email address.", vm.ErrorMessage);
        Assert.False(_navigatedToProfileSetup);
        await _authService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_SetsErrorMessage()
    {
        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "pass123";
        vm.ConfirmPassword = "pass123";

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AuthInputValidator.PasswordRequirementsMessage, vm.ErrorMessage);
        Assert.False(_navigatedToProfileSetup);
        await _authService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_SetsErrorMessage()
    {
        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "Pass123!";
        vm.ConfirmPassword = "Different123!";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Passwords do not match", vm.ErrorMessage);
        Assert.False(_navigatedToProfileSetup);
        await _authService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_RegistrationFails_SetsErrorMessage()
    {
        _authService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((User?)null);

        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "Pass123!";
        vm.ConfirmPassword = "Pass123!";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Registration failed", vm.ErrorMessage);
        Assert.False(_navigatedToProfileSetup);
    }
}
