using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Registration;

public sealed class AccountRegistrationUseCase(IAccountRegistrationGateway gateway)
    : IAccountRegistrationUseCase
{
    public async Task<StartAccountRegistrationResult> StartAsync(
        StartAccountRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PrivacyConsent is not { } privacyConsent ||
            !privacyConsent.Accepted || privacyConsent.NoticeVersion != PrivacyNotice.CurrentVersion)
            return new StartAccountRegistrationResult(AccountRegistrationStatus.PrivacyConsentRequired,
                "Review and accept the privacy notice before creating an account.");
        var email = request.Email?.Trim() ?? string.Empty;
        if (!AuthInputValidator.IsValidEmail(email))
            return new StartAccountRegistrationResult(AccountRegistrationStatus.InvalidEmail,
                "Enter a valid email address.");

        if (await gateway.EmailExistsAsync(email, cancellationToken).ConfigureAwait(false))
            return new StartAccountRegistrationResult(AccountRegistrationStatus.ExistingEmail,
                ExistingEmail: email.ToLowerInvariant());

        cancellationToken.ThrowIfCancellationRequested();
        var username = request.Username?.Trim() ?? string.Empty;
        if (!AuthInputValidator.IsValidUsername(username))
            return new StartAccountRegistrationResult(AccountRegistrationStatus.InvalidUsername,
                AuthInputValidator.UsernameRequirementsMessage);

        if (await gateway.UsernameExistsAsync(username, cancellationToken).ConfigureAwait(false))
            return new StartAccountRegistrationResult(AccountRegistrationStatus.UsernameTaken,
                "This username is already taken. Please choose another.");

        cancellationToken.ThrowIfCancellationRequested();
        if (!AuthInputValidator.TryValidatePasswordConfirmation(
                request.Password, request.ConfirmPassword, out var passwordError, "Passwords do not match"))
            return new StartAccountRegistrationResult(AccountRegistrationStatus.InvalidPassword, passwordError);

        var challenge = await gateway.StageAndStartChallengeAsync(
            email, username, request.Password, cancellationToken, privacyConsent).ConfigureAwait(false);
        if (challenge.ReplacedOrCancelled)
            return new StartAccountRegistrationResult(AccountRegistrationStatus.ReplacedOrCancelled);
        return challenge.Started
            ? new StartAccountRegistrationResult(AccountRegistrationStatus.Started,
                AttemptId: challenge.AttemptId)
            : new StartAccountRegistrationResult(AccountRegistrationStatus.ChallengeFailed,
                challenge.ErrorMessage ?? "Could not send verification code. Please try again.");
    }

    public void CancelAttempt(Guid attemptId)
    {
        gateway.CancelAttempt(attemptId);
    }

    public void HandOffAttempt(Guid attemptId)
    {
        gateway.HandOffAttempt(attemptId);
    }
}
