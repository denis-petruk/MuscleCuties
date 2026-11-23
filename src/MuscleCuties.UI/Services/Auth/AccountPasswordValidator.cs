using MuscleCuties.Core.Domain.Auth;

namespace MuscleCuties.UI.Services.Auth;

public interface IAccountPasswordValidator
{
    bool TryValidateConfirmation(string? password, string? confirmation, out string errorMessage);
    bool IsValidEmail(string? email);
    bool TryValidateOtpCode(string? code, out string normalizedCode);
    string SanitizeTypedOtpCode(string? value);
}

public sealed class AccountPasswordValidator : IAccountPasswordValidator
{
    public bool TryValidateConfirmation(string? password, string? confirmation, out string errorMessage)
    {
        return AuthInputValidator.TryValidatePasswordConfirmation(password, confirmation, out errorMessage);
    }

    public bool IsValidEmail(string? email)
    {
        return AuthInputValidator.IsValidEmail(email);
    }

    public bool TryValidateOtpCode(string? code, out string normalizedCode)
    {
        return AuthInputValidator.TryValidateOtpCode(code, out normalizedCode, out _);
    }

    public string SanitizeTypedOtpCode(string? value)
    {
        return AuthInputValidator.SanitizeTypedOtpCode(value);
    }
}
