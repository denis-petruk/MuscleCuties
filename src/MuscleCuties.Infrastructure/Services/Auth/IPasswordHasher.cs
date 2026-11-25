namespace MuscleCuties.Core.Services.Auth;

/// <summary>Hashes and verifies passwords, so no caller ever stores a plain one.</summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    (bool IsValid, bool NeedsMigration) VerifyPassword(string password, string? storedHash);
}
