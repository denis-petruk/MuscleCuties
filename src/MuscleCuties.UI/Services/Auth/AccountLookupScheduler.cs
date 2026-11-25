using MuscleCuties.Core.Data;
using MuscleCuties.UseCases.Features.Account.Registration;

namespace MuscleCuties.UI.Services.Auth;

/// <summary>Availability lookups (email, username) serialised the same way as other account calls.</summary>
public sealed class AccountLookupScheduler : IAccountLookupScheduler
{
    /// <summary>Runs one availability check after any in-flight load finishes.</summary>
    public Task<bool> RunAsync(Func<Task<bool>> operation)
    {
        return DataLoadScheduler.RunAsync(operation);
    }
}
