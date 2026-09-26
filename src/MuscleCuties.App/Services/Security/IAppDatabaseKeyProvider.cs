namespace MuscleCuties.App.Services.Security;

public interface IAppDatabaseKeyProvider
{
    Task InitializeAsync();
    string GetDatabasePassword();
}
