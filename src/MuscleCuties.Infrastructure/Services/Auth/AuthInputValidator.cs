namespace MuscleCuties.Core.Services.Auth;

public static class AuthInputValidator
{
    public const string PasswordRequirementsMessage = Domain.Auth.AuthInputValidator.PasswordRequirementsMessage;
    public const string UsernameRequirementsMessage = Domain.Auth.AuthInputValidator.UsernameRequirementsMessage;
    public const string OtpCodeRequirementsMessage = Domain.Auth.AuthInputValidator.OtpCodeRequirementsMessage;

    public static string NormalizeEmail(string? email)
    {
        return Domain.Auth.AuthInputValidator.NormalizeEmail(email);
    }

    public static string NormalizeOtpCode(string? code)
    {
        return Domain.Auth.AuthInputValidator.NormalizeOtpCode(code);
    }

    public static bool IsValidOtpCode(string? code)
    {
        return Domain.Auth.AuthInputValidator.IsValidOtpCode(code);
    }

    public static bool TryValidateOtpCode(string? code, out string normalizedCode, out string errorMessage)
    {
        return Domain.Auth.AuthInputValidator.TryValidateOtpCode(code, out normalizedCode, out errorMessage);
    }

    public static bool IsValidUsername(string? username)
    {
        return Domain.Auth.AuthInputValidator.IsValidUsername(username);
    }

    public static bool IsValidEmail(string? email)
    {
        return Domain.Auth.AuthInputValidator.IsValidEmail(email);
    }

    public static bool IsStrongPassword(string? password)
    {
        return Domain.Auth.AuthInputValidator.IsStrongPassword(password);
    }

    public static bool TryValidatePasswordConfirmation(string? password, string? confirmPassword,
        out string errorMessage, string mismatchMessage = "Passwords do not match.")
    {
        return Domain.Auth.AuthInputValidator.TryValidatePasswordConfirmation(password, confirmPassword,
            out errorMessage, mismatchMessage);
    }
}
