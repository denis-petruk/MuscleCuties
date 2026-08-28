using System.Net.Mail;

namespace MuscleCuties.Core.Services.Auth;

public static class AuthInputValidator
{
    public const string PasswordRequirementsMessage =
        "Password needs 8+ characters with uppercase, lowercase, number, and symbol.";

    public static bool IsValidEmail(string? email)
    {
        var value = email?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254 || value.Any(char.IsWhiteSpace))
            return false;

        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase) &&
                   address.Host.Contains('.', StringComparison.Ordinal) &&
                   !address.Host.StartsWith(".", StringComparison.Ordinal) &&
                   !address.Host.EndsWith(".", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Any(char.IsWhiteSpace))
            return false;

        return password.Any(char.IsLower) &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsDigit) &&
               password.Any(character => !char.IsLetterOrDigit(character));
    }
}
