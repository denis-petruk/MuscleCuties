namespace MuscleCuties.Core.Services.Auth;

/// <summary>The input rules for the auth forms, shared by the UI and the services so both accept the same thing.</summary>
public static class AuthInputValidator
{
    public const string PasswordRequirementsMessage = Domain.Auth.AuthInputValidator.PasswordRequirementsMessage;
    public const string UsernameRequirementsMessage = Domain.Auth.AuthInputValidator.UsernameRequirementsMessage;
    public const string OtpCodeRequirementsMessage = Domain.Auth.AuthInputValidator.OtpCodeRequirementsMessage;

    /// <summary>An address in the form the backend stores, so the same address cannot create two accounts by casing.</summary>
    public static string NormalizeEmail(string? email)
    {
        return Domain.Auth.AuthInputValidator.NormalizeEmail(email);
    }

    /// <summary>A code with its spacing removed, since users paste it with spaces.</summary>
    public static string NormalizeOtpCode(string? code)
    {
        return Domain.Auth.AuthInputValidator.NormalizeOtpCode(code);
    }

    /// <summary>Whether a code has the length the backend issues.</summary>
    public static bool IsValidOtpCode(string? code)
    {
        return Domain.Auth.AuthInputValidator.IsValidOtpCode(code);
    }

    /// <summary>The same check, returning the normalised code so the caller does not normalise twice.</summary>
    public static bool TryValidateOtpCode(string? code, out string normalizedCode, out string errorMessage)
    {
        return Domain.Auth.AuthInputValidator.TryValidateOtpCode(code, out normalizedCode, out errorMessage);
    }

    /// <summary>The username rule, which is stricter than non-empty because it appears in the UI.</summary>
    public static bool IsValidUsername(string? username)
    {
        return Domain.Auth.AuthInputValidator.IsValidUsername(username);
    }

    /// <summary>A shape check for an address, catching typos before a round trip.</summary>
    public static bool IsValidEmail(string? email)
    {
        return Domain.Auth.AuthInputValidator.IsValidEmail(email);
    }

    /// <summary>The password rule the registration form enforces, so a weak password is refused before it is sent.</summary>
    public static bool IsStrongPassword(string? password)
    {
        return Domain.Auth.AuthInputValidator.IsStrongPassword(password);
    }

    /// <summary>True when the repeated password matches, reporting why it does not.</summary>
    public static bool TryValidatePasswordConfirmation(string? password, string? confirmPassword,
        out string errorMessage, string mismatchMessage = "Passwords do not match.")
    {
        return Domain.Auth.AuthInputValidator.TryValidatePasswordConfirmation(password, confirmPassword,
            out errorMessage, mismatchMessage);
    }
}
