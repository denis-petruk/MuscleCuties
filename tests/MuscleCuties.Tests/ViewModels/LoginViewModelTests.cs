using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Tests.ViewModels;

public class LoginViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IQuizService _quizService = Substitute.For<IQuizService>();
    private bool _navigatedToDashboard;
    private bool _navigatedToQuiz;

    private LoginViewModel CreateViewModel() =>
        new(_authService, _quizService, () => _navigatedToDashboard = true, () => _navigatedToQuiz = true, () => { });

    [Fact]
    public async Task LoginAsync_ValidCredentials_OnboardingComplete_NavigatesToDashboard()
    {
        var user = new User { Id = 1, Email = "test@test.com", PasswordHash = "hash" };
        _authService.LoginAsync("test@test.com", "pass").Returns(user);
        _quizService.IsOnboardingCompleteAsync(1).Returns(true);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(_navigatedToDashboard);
        Assert.False(_navigatedToQuiz);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_OnboardingIncomplete_NavigatesToQuiz()
    {
        var user = new User { Id = 2, Email = "test@test.com", PasswordHash = "hash" };
        _authService.LoginAsync("test@test.com", "pass").Returns(user);
        _quizService.IsOnboardingCompleteAsync(2).Returns(false);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(_navigatedToDashboard);
        Assert.True(_navigatedToQuiz);
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
        Assert.False(_navigatedToQuiz);
    }

    [Fact]
    public async Task LoginAsync_SetsAndClearsBusy()
    {
        var user = new User { Id = 1, Email = "test@test.com", PasswordHash = "hash" };
        _authService.LoginAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(user);
        _quizService.IsOnboardingCompleteAsync(Arg.Any<int>()).Returns(true);

        var vm = CreateViewModel();
        vm.Email = "test@test.com";
        vm.Password = "pass";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
    }
}
