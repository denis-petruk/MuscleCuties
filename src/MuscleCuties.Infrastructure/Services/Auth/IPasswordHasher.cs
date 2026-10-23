namespace MuscleCuties.Core.Services.Auth;

public interface IPasswordHasher
{
    string HashPassword(string password);
    (bool IsValid, bool NeedsMigration) VerifyPassword(string password, string? storedHash);
}
