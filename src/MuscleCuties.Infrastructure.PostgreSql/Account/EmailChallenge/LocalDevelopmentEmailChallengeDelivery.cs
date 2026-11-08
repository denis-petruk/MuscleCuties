using MuscleCuties.UseCases.Features.Account.EmailChallenge;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;

/// <summary>
/// Development-only delivery channel for local, disposable backends. It writes the verification code to the
/// process log so a developer can complete registration without an email provider. It never sends mail and is
/// only registered when the host runs in the Development environment with no configured provider.
/// </summary>
public sealed class LocalDevelopmentEmailChallengeDelivery : IEmailChallengeDelivery
{
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;

    public Task<bool> SendAsync(
        string normalizedEmail, string code, int purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(code))
            return Task.FromResult(false);

        var purposeName = purpose switch
        {
            RegistrationPurpose => "registration",
            PasswordRecoveryPurpose => "password-recovery",
            _ => null
        };
        if (purposeName is null)
            return Task.FromResult(false);

        Console.WriteLine(
            $"[local-dev-email] {purposeName} code for {normalizedEmail}: {code} (development delivery, nothing was sent)");
        return Task.FromResult(true);
    }
}
