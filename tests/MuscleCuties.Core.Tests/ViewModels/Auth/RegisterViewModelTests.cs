using NSubstitute;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Tests.ViewModels.Auth;

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
        _authService.RegisterAsync("new@test.com", "Pass123!").Returns(user);

        var vm = CreateViewModel();
        vm.Email = "new@test.com";
        vm.Password = "Pass123!";
        vm.ConfirmPassword = "Pass123!";
        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(_navigatedToQuiz);
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
        Assert.False(_navigatedToQuiz);
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
        Assert.False(_navigatedToQuiz);
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
        Assert.False(_navigatedToQuiz);
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
        Assert.False(_navigatedToQuiz);
    }
}
