namespace MuscleCuties.Core.Models.Enums.Users;

/// <summary>How an account authenticates, which decides the sign-in path it may use.</summary>
public enum UserCredentialType
{
    Password = 0,
    Apple = 1
}
