namespace MuscleCuties.Core.Services.Cycle;

public interface ICycleAccountService
{
    Task<DateTime?> GetAccountCreatedDateAsync(int userId);
}
