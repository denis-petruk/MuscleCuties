using System.Collections.Concurrent;
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

    // Development-only hand-off for automated journeys: the code a local backend just generated for
    // this address. It exists only while this Development delivery is registered; nothing persists
    // and no other environment can read it.
    private static readonly ConcurrentDictionary<string, string> LatestCodes = new(StringComparer.Ordinal);

    public static bool TryGetLatestCode(string normalizedEmail, int purpose, out string code)
    {
        code = string.Empty;
        return !string.IsNullOrWhiteSpace(normalizedEmail) &&
               LatestCodes.TryGetValue(Key(normalizedEmail, purpose), out code!);
    }

    private static string Key(string normalizedEmail, int purpose) =>
        $"{purpose}:{normalizedEmail.Trim().ToLowerInvariant()}";

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

        LatestCodes[Key(normalizedEmail, purpose)] = code;
        Console.WriteLine(
            $"[local-dev-email] {purposeName} code for {normalizedEmail}: {code} (development delivery, nothing was sent)");
        return Task.FromResult(true);
    }
}
