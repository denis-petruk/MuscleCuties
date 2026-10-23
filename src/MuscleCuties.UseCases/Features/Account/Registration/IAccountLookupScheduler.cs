namespace MuscleCuties.UseCases.Features.Account.Registration;

public interface IAccountLookupScheduler
{
    Task<bool> RunAsync(Func<Task<bool>> operation);
}
