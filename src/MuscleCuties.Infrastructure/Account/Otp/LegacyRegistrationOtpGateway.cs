using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;

namespace MuscleCuties.Infrastructure.Account.Otp;

public sealed class LegacyRegistrationOtpGateway(
    IPendingRegistrationService pending,
    IServiceScopeFactory scopeFactory) : IRegistrationOtpGateway
{
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    public RegistrationOtpSession OpenSession()
    {
        var registration = pending.CurrentRegistration;
        if (registration is null || string.IsNullOrWhiteSpace(registration.ChallengeId))
            return new RegistrationOtpSession(Guid.Empty, registration?.Email ?? string.Empty, false);

        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = new SessionState(registration);
        return new RegistrationOtpSession(sessionId, registration.Email, true);
    }

    public async Task<VerifyRegistrationOtpResult> VerifyAsync(
        Guid sessionId, string code, CancellationToken cancellationToken)
    {
        if (!TryGetActive(sessionId, out var state, out var registration))
            return new VerifyRegistrationOtpResult(false, true);

        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEmailChallengeService>()
            .VerifyChallengeAsync(registration.ChallengeId!, registration.Email, code,
                EmailChallengePurpose.Registration, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.SyncRoot)
        {
            if (state.Closed || state.Verified ||
                !ReferenceEquals(registration, state.Registration) ||
                !ReferenceEquals(registration, pending.CurrentRegistration))
                return new VerifyRegistrationOtpResult(false, true);

            if (!result.Succeeded)
                return new VerifyRegistrationOtpResult(false, false,
                    result.ErrorMessage ?? "Invalid code. Please check and try again.");
            if (string.IsNullOrWhiteSpace(result.VerificationToken))
                return new VerifyRegistrationOtpResult(false, false,
                    "Verification could not be completed. Please try again.");
            state.VerificationToken = result.VerificationToken;
            return new VerifyRegistrationOtpResult(true, false);
        }
    }

    public bool ConfirmSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return false;
        lock (state.SyncRoot)
        {
            if (state.Closed || state.Verified ||
                string.IsNullOrWhiteSpace(state.VerificationToken) ||
                !ReferenceEquals(state.Registration, pending.CurrentRegistration) ||
                !pending.TryConfirmChallenge(state.Registration, state.VerificationToken))
                return false;

            state.VerificationToken = null;
            state.Verified = true;
            return true;
        }
    }

    public async Task<ResendRegistrationOtpResult> ResendAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetActive(sessionId, out var state, out var registration))
            return new ResendRegistrationOtpResult(false, true);

        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IEmailChallengeService>()
            .ResendChallengeAsync(registration.ChallengeId!, registration.Email,
                EmailChallengePurpose.Registration, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        lock (state.SyncRoot)
        {
            if (state.Closed || state.Verified ||
                !ReferenceEquals(registration, state.Registration) ||
                !ReferenceEquals(registration, pending.CurrentRegistration))
                return new ResendRegistrationOtpResult(false, true);

            if (!result.Succeeded)
                return new ResendRegistrationOtpResult(false, false, ErrorMessage:
                    result.ErrorMessage ?? "Could not resend code. Please try again.");
            if (string.IsNullOrWhiteSpace(result.ChallengeId) ||
                string.Equals(registration.ChallengeId, result.ChallengeId, StringComparison.Ordinal))
                return new ResendRegistrationOtpResult(false, false, ErrorMessage:
                    "A new verification session could not be started. Please try again.");

            if (!pending.TryUpdateChallengeId(registration, result.ChallengeId, out var updated) ||
                updated is null)
                return new ResendRegistrationOtpResult(false, true);

            state.Registration = updated;
            state.VerificationToken = null;
            return new ResendRegistrationOtpResult(true, false,
                result.ResendCooldownSeconds > 0 ? result.ResendCooldownSeconds : 60);
        }
    }

    public void CancelSession(Guid sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var state))
            return;
        lock (state.SyncRoot)
        {
            state.Closed = true;
            if (!state.Verified)
                pending.TryClear(state.Registration);
        }
    }

    public void HandOffSession(Guid sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var state))
            return;
        lock (state.SyncRoot)
        {
            state.Closed = true;
        }
    }

    private bool TryGetActive(Guid sessionId, out SessionState state, out PendingRegistrationData registration)
    {
        if (!_sessions.TryGetValue(sessionId, out state!))
        {
            registration = null!;
            return false;
        }

        lock (state.SyncRoot)
        {
            if (state.Closed || state.Verified ||
                !ReferenceEquals(state.Registration, pending.CurrentRegistration) ||
                string.IsNullOrWhiteSpace(state.Registration.ChallengeId))
            {
                registration = null!;
                return false;
            }

            registration = state.Registration;
            return true;
        }
    }

    private sealed class SessionState(PendingRegistrationData registration)
    {
        public object SyncRoot { get; } = new();
        public PendingRegistrationData Registration { get; set; } = registration;
        public bool Closed { get; set; }
        public bool Verified { get; set; }
        public string? VerificationToken { get; set; }
    }
}
