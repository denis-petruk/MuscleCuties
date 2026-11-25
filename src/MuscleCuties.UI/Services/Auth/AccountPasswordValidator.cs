using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.UI.Services.Auth;

/// <summary>Form-level credential checks, so the screens share one rule set instead of repeating it.</summary>
public interface IAccountPasswordValidator
{
    bool TryValidateConfirmation(string? password, string? confirmation, out string errorMessage);
    bool IsValidEmail(string? email);
}

/// <summary>Delegates password confirmation and email shape to the shared auth validator.</summary>
public sealed class AccountPasswordValidator : IAccountPasswordValidator
{
    /// <summary>Checks the two passwords match and are strong enough, returning the message to show instead</summary>
    /// of a bare false.</summary>
    public bool TryValidateConfirmation(string? password, string? confirmation, out string errorMessage)
    {
        return AuthInputValidator.TryValidatePasswordConfirmation(password, confirmation, out errorMessage);
    }

    /// <summary>True when the address is well formed, for enabling the submit action before sending.</summary>
    public bool IsValidEmail(string? email)
    {
        return AuthInputValidator.IsValidEmail(email);
    }
}
