using MuscleCuties.Core.Data;
using MuscleCuties.UseCases.Features.Account.Registration;

namespace MuscleCuties.UI.Services.Auth;

public sealed class AccountLookupScheduler : IAccountLookupScheduler
{
    public Task<bool> RunAsync(Func<Task<bool>> operation)
    {
        return DataLoadScheduler.RunAsync(operation);
    }
}
