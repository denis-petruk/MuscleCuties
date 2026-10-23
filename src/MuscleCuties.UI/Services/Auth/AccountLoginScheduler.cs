using MuscleCuties.Core.Data;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UI.Services.Auth;

public interface IAccountLoginScheduler
{
    Task<AccountSignInResult> RunAsync(Func<Task<AccountSignInResult>> operation);
}

public sealed class AccountLoginScheduler : IAccountLoginScheduler
{
    public Task<AccountSignInResult> RunAsync(Func<Task<AccountSignInResult>> operation)
    {
        return DataLoadScheduler.RunAsync(operation);
    }
}
