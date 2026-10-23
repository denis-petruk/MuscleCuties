using System.Security.Cryptography;
using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class EmailReceiptLifetimeTests
{
    private const int SyntheticKeyBits = 2048;
    private const int SimulatedQuizMinutes = 20;
    private const string SyntheticKeyId = "synthetic-key";
    private const string SyntheticIssuer = "synthetic-issuer";
    private const string SyntheticAudience = "synthetic-audience";
    private const string SyntheticEmail = "registration@example.invalid";

    [Fact]
    public void VerifiedRegistrationSurvivesQuizAndExpiresAtOnboardingLimit()
    {
        using var key = RSA.Create(SyntheticKeyBits);
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var challengeId = Guid.NewGuid().ToString("D");
        var receiptId = Guid.NewGuid().ToString("N");
        var issuer = new SignedEmailReceiptTokenIssuer(SyntheticKeyId, key.ExportRSAPrivateKeyPem(),
            SyntheticIssuer, SyntheticAudience, clock);
        var verifier = new SignedRegistrationEmailReceiptVerifier(
            new Dictionary<string, string> { [SyntheticKeyId] = key.ExportRSAPublicKeyPem() },
            SyntheticIssuer, SyntheticAudience, clock);
        var lifetime = EmailReceiptLifetimePolicy.ForPurpose(EmailReceiptLifetimePolicy.RegistrationPurpose);
        var token = issuer.Issue(SyntheticEmail, EmailReceiptLifetimePolicy.RegistrationPurpose,
            challengeId, receiptId, clock.GetUtcNow().Add(lifetime));

        clock.Advance(TimeSpan.FromMinutes(SimulatedQuizMinutes));
        Assert.True(verifier.TryVerify(token, SyntheticEmail, challengeId, out var receipt));
        Assert.Equal(receiptId, receipt?.Id);

        clock.Advance(lifetime - TimeSpan.FromMinutes(SimulatedQuizMinutes));
        Assert.False(verifier.TryVerify(token, SyntheticEmail, challengeId, out _));
    }

    [Fact]
    public void PasswordRecoveryCannotUseRegistrationLifetime()
    {
        using var key = RSA.Create(SyntheticKeyBits);
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var challengeId = Guid.NewGuid().ToString("D");
        var receiptId = Guid.NewGuid().ToString("N");
        var issuer = new SignedEmailReceiptTokenIssuer(SyntheticKeyId, key.ExportRSAPrivateKeyPem(),
            SyntheticIssuer, SyntheticAudience, clock);

        Assert.Throws<InvalidOperationException>(() => issuer.Issue(SyntheticEmail,
            EmailReceiptLifetimePolicy.PasswordRecoveryPurpose, challengeId, receiptId,
            clock.GetUtcNow().Add(EmailReceiptLifetimePolicy.ForPurpose(EmailReceiptLifetimePolicy.RegistrationPurpose))));

        var recoveryToken = issuer.Issue(SyntheticEmail,
            EmailReceiptLifetimePolicy.PasswordRecoveryPurpose, challengeId, receiptId,
            clock.GetUtcNow().Add(EmailReceiptLifetimePolicy.ForPurpose(EmailReceiptLifetimePolicy.PasswordRecoveryPurpose)));
        var recoveryVerifier = new SignedRegistrationEmailReceiptVerifier(
            new Dictionary<string, string> { [SyntheticKeyId] = key.ExportRSAPublicKeyPem() },
            SyntheticIssuer, SyntheticAudience, clock, EmailReceiptLifetimePolicy.PasswordRecoveryPurpose);

        Assert.True(recoveryVerifier.TryVerify(recoveryToken, SyntheticEmail, challengeId, out _));
        clock.Advance(EmailReceiptLifetimePolicy.ForPurpose(EmailReceiptLifetimePolicy.PasswordRecoveryPurpose));
        Assert.False(recoveryVerifier.TryVerify(recoveryToken, SyntheticEmail, challengeId, out _));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
