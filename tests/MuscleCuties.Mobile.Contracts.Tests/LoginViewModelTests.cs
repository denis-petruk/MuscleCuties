using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.ViewModels.Auth;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task PasswordReturnCommandCannotStartAnotherSignInWhileAppleIsPending()
    {
        var platformSignIn = new PendingPlatformSignIn();
        var signIn = new RecordingSignInUseCase();
        var viewModel = new LoginViewModel(signIn, new InlineLoginScheduler(),
            () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask, platformSignIn)
        {
            Email = "qa@example.test",
            Password = "synthetic-password"
        };

        var pending = viewModel.SignInWithAppleCommand.ExecuteAsync(null);
        var canExecuteDuringPlatformSignIn = viewModel.LoginCommand.CanExecute(null);
        await viewModel.LoginCommand.ExecuteAsync(null);
        platformSignIn.Cancel();
        await pending;

        Assert.Equal(0, signIn.PasswordAttempts);
        Assert.False(canExecuteDuringPlatformSignIn);
        Assert.True(viewModel.LoginCommand.CanExecute(null));
    }

    private sealed class RecordingSignInUseCase : IAccountSignInUseCase
    {
        public int PasswordAttempts { get; private set; }

        public Task<AccountSignInResult> SignInWithPasswordAsync(PasswordSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            PasswordAttempts++;
            return Task.FromResult(new AccountSignInResult(true, IsOnboardingComplete: true));
        }

        public Task<AccountSignInResult> SignInWithPlatformAsync(PlatformSignInRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InlineLoginScheduler : IAccountLoginScheduler
    {
        public Task<AccountSignInResult> RunAsync(Func<Task<AccountSignInResult>> operation) => operation();
    }

    private sealed class PendingPlatformSignIn : IPlatformAccountSignInAdapter
    {
        private readonly TaskCompletionSource<PlatformSignInRequest?> _request =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsAvailable => true;
        public string ProviderName => "Apple";
        public string LoginButtonText => "Sign in with Apple";
        public string RegisterButtonText => "Sign up with Apple";

        public Task<PlatformSignInRequest?> SignInAsync() => _request.Task;

        public void Cancel() => _request.SetResult(null);
    }
}
