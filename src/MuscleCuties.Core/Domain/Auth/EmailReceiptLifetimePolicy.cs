namespace MuscleCuties.Core.Domain.Auth;

/// <summary>Takes the clock, so receipt expiry is measured rather than assumed.</summary>
public static class EmailReceiptLifetimePolicy
{
    public const int RegistrationPurpose = 1;
    public const int PasswordRecoveryPurpose = 2;
    public const int RegistrationLifetimeHours = 24;
    public const int PasswordRecoveryLifetimeMinutes = 10;

    /// <summary>The lifetime a verification receipt for the given purpose stays valid.</summary>
    public static TimeSpan ForPurpose(int purpose) => purpose switch
    {
        RegistrationPurpose => TimeSpan.FromHours(RegistrationLifetimeHours),
        PasswordRecoveryPurpose => TimeSpan.FromMinutes(PasswordRecoveryLifetimeMinutes),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };
}
