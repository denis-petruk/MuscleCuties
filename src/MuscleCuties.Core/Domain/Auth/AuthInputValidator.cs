namespace MuscleCuties.Core.Domain.Auth;

public static class AuthInputValidator
{
    public const string PasswordRequirementsMessage =
        "Password needs 8+ characters with uppercase, lowercase, number, and symbol.";

    public const string UsernameRequirementsMessage =
        "Username must be 3-30 characters using letters, numbers, underscores, or hyphens.";

    public const string OtpCodeRequirementsMessage =
        "Please enter a valid 6-digit verification code.";

    public static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public static string NormalizeOtpCode(string? code)
    {
        return code?.Trim().Replace(" ", "", StringComparison.Ordinal) ?? string.Empty;
    }

    public static bool IsValidOtpCode(string? code)
    {
        var cleanCode = NormalizeOtpCode(code);
        return cleanCode.Length == 6 && cleanCode.All(static digit => digit is >= '0' and <= '9');
    }

    public static bool TryValidateOtpCode(string? code, out string normalizedCode, out string errorMessage)
    {
        normalizedCode = NormalizeOtpCode(code);
        if (normalizedCode.Length != 6 || !normalizedCode.All(static digit => digit is >= '0' and <= '9'))
        {
            errorMessage = OtpCodeRequirementsMessage;
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    // Validates username format: 3-30 characters, alphanumeric, underscores, hyphens, periods.
    public static bool IsValidUsername(string? username)
    {
        var value = username?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 3 or > 30 || value.Any(char.IsWhiteSpace))
            return false;

        if (value.StartsWith('.') || value.EndsWith('.') || value.StartsWith('-') || value.EndsWith('-'))
            return false;

        return value.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.');
    }

    public static bool IsValidEmail(string? email)
    {
        var value = email?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254 || value.Any(char.IsWhiteSpace))
            return false;

        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex != value.LastIndexOf('@') || atIndex == value.Length - 1)
            return false;

        var user = value[..atIndex];
        var host = value[(atIndex + 1)..];

        if (user.Length > 64 || user.StartsWith('.') || user.EndsWith('.') ||
            user.Contains("..", StringComparison.Ordinal))
            return false;

        if (host.Length < 3 || host.StartsWith('.') || host.EndsWith('.') ||
            host.Contains("..", StringComparison.Ordinal))
            return false;

        var dotIndex = host.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0 || dotIndex == host.Length - 1)
            return false;

        return true;
    }

    // Enforces 8+ non-whitespace characters with at least one lowercase, uppercase, digit, and symbol.
    public static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Any(char.IsWhiteSpace))
            return false;

        return password.Any(char.IsLower) &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsDigit) &&
               password.Any(character => !char.IsLetterOrDigit(character));
    }

    public static bool TryValidatePasswordConfirmation(
        string? password,
        string? confirmPassword,
        out string errorMessage,
        string mismatchMessage = "Passwords do not match.")
    {
        if (!IsStrongPassword(password))
        {
            errorMessage = PasswordRequirementsMessage;
            return false;
        }

        if (password != confirmPassword)
        {
            errorMessage = mismatchMessage;
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
