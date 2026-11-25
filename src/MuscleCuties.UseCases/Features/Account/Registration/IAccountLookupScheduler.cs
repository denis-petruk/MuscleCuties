namespace MuscleCuties.UseCases.Features.Account.Registration;

/// <summary>Runs the existence checks off the request thread so the form does not stall.</summary>
public interface IAccountLookupScheduler
{
    Task<bool> RunAsync(Func<Task<bool>> operation);
}
