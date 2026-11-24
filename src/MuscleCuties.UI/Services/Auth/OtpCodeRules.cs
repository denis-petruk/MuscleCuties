namespace MuscleCuties.UI.Services.Auth;

/// <summary>
/// One rule for OTP input, shared by OTP verification and password recovery: keep digits only,
/// capped at the six-digit code length the backend issues.
/// </summary>
public static class OtpCodeRules
{
    public const int CodeLength = 6;

    public static string DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).Take(CodeLength).ToArray());
    }
}
