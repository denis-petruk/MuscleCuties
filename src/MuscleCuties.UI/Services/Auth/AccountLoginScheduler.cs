using MuscleCuties.Core.Data;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.Services.Auth;

/// <summary>Runs a sign-in call through the shared load scheduler, so a login started twice cannot race</summary>
/// the same session state.</summary>
public interface IAccountLoginScheduler
{
    Task<AccountSignInResult> RunAsync(Func<Task<AccountSignInResult>> operation);
}

/// <summary>Sign-in calls serialised on the shared data-load scheduler.</summary>
public sealed class AccountLoginScheduler : IAccountLoginScheduler
{
    /// <summary>Waits for any in-flight load, then runs the sign-in and returns its outcome unchanged.</summary>
    public Task<AccountSignInResult> RunAsync(Func<Task<AccountSignInResult>> operation)
    {
        return DataLoadScheduler.RunAsync(operation);
    }
}
