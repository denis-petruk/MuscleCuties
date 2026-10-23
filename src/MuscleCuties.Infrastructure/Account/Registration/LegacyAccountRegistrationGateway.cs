using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.Infrastructure.Account.Registration;

// Keeps pending credentials in the existing in-memory local service and sends only
// the account email to the verification service. No health or profile data leaves the device.
public sealed class LegacyAccountRegistrationGateway(
    IServiceScopeFactory scopeFactory,
    IPendingRegistrationService pending,
    IAccountLookupScheduler lookupScheduler)
    : IAccountRegistrationGateway
{
    private readonly ConcurrentDictionary<Guid, PendingRegistrationData> _activeAttempts = new();

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        return await lookupScheduler.RunAsync(() => scope.ServiceProvider
            .GetRequiredService<IAuthService>().EmailExistsAsync(email)).ConfigureAwait(false);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        return await lookupScheduler.RunAsync(() => scope.ServiceProvider
            .GetRequiredService<IAuthService>().UsernameExistsAsync(username)).ConfigureAwait(false);
    }

    public async Task<RegistrationChallengeOutcome> StageAndStartChallengeAsync(
        string email, string username, string password, CancellationToken cancellationToken,
        RegistrationPrivacyConsent? privacyConsent = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IEmailChallengeService>();
        var registration = pending.StartEmailRegistration(email, username, password, null,
            privacyConsent?.NoticeVersion, privacyConsent?.AcceptedAtUtc);
        var handedOff = false;
        try
        {
            var result = await challengeService.StartChallengeAsync(
                email, EmailChallengePurpose.Registration, cancellationToken, privacyConsent).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(registration, pending.CurrentRegistration))
                return new RegistrationChallengeOutcome(false, true);

            if (!result.Succeeded)
                return new RegistrationChallengeOutcome(false, ErrorMessage:
                    result.ErrorMessage ?? "Could not send verification code. Please try again.");
            if (string.IsNullOrWhiteSpace(result.ChallengeId))
                return new RegistrationChallengeOutcome(false, ErrorMessage:
                    "Could not start email verification. Please try again.");

            if (!pending.TryUpdateChallengeId(registration, result.ChallengeId, out _))
                return new RegistrationChallengeOutcome(false, true);

            var attemptId = Guid.NewGuid();
            _activeAttempts[attemptId] = registration;
            handedOff = true;
            return new RegistrationChallengeOutcome(true, AttemptId: attemptId);
        }
        finally
        {
            if (!handedOff)
                pending.TryClear(registration);
        }
    }

    public void CancelAttempt(Guid attemptId)
    {
        if (_activeAttempts.TryRemove(attemptId, out var registration))
            pending.TryClear(registration);
    }

    public void HandOffAttempt(Guid attemptId)
    {
        _activeAttempts.TryRemove(attemptId, out _);
    }
}
