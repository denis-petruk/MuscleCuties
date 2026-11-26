namespace MuscleCuties.UI.Services.Auth;

/// <summary>
/// One rule for OTP input, shared by OTP verification and password recovery: keep digits only,
/// capped at the six-digit code length the backend issues.
/// </summary>
public static class OtpCodeRules
{
    public const int CodeLength = 6;

    /// <summary>Strips everything but digits and caps the result at the code length, so pasting a code with</summary>
    /// spaces or a country prefix still fills the field.</summary>
    public static string DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).Take(CodeLength).ToArray());
    }

    /// <summary>Returns the rewritten digits when the input differs from them, or null when it is already clean,
    /// so both OTP screens normalize a pasted code without duplicating the comparison.</summary>
    public static string? RewriteToDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var digits = DigitsOnly(value);
        return string.Equals(digits, value, StringComparison.Ordinal) ? null : digits;
    }
}
