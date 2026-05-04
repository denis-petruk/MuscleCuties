using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Tests.ViewModels;

public class RegisterViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private bool _navigatedToQuiz;

    private RegisterViewModel CreateViewModel() =>
        new(_authService, () => _navigatedToQuiz = true, () => { });

    [Fact]
    public async Task RegisterAsync_ValidCredentials_NavigatesToQuiz()
    {
        var user = new User { Id = 1, Email = "new@test.com", PasswordHash = "hash" };
        _authService.RegisterAsync("new@test.com", "pass123").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "pass123";
        vm.ConfirmPassword = "pass123";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(_navigatedToQuiz);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_SetsErrorMessage()
    {
        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "pass123";
        vm.ConfirmPassword = "different";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Passwords do not match", vm.ErrorMessage);
        Assert.False(_navigatedToQuiz);
        await _authService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_RegistrationFails_SetsErrorMessage()
    {
        _authService.RegisterAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((User?)null);

        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "pass123";
        vm.ConfirmPassword = "pass123";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Registration failed", vm.ErrorMessage);
        Assert.False(_navigatedToQuiz);
    }
}
