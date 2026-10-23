using MuscleCuties.Core.Services.Auth;

namespace MuscleCuties.UI.Services.Auth;

public interface IAccountPasswordValidator
{
    bool TryValidateConfirmation(string? password, string? confirmation, out string errorMessage);
    bool IsValidEmail(string? email);
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
}
