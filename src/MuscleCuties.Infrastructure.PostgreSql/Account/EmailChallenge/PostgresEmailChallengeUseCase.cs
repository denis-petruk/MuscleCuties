using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;

public sealed class PostgresEmailChallengeUseCase : IEmailChallengeUseCase
{
    private const int RegistrationPurpose = 1;
    private const int PasswordRecoveryPurpose = 2;
    private const int OtpCodeDigits = 6;
    private const int MaximumOtpCodeValueExclusive = 1_000_000;
    private const int CodeLifetimeMinutes = 10;
    private const int ResendCooldownSeconds = 60;
    private const int MaximumChallengeAttempts = 5;
    private const int MaximumChallengesPerEmailPerDay = 5;
    private const int MaximumChallengesGloballyPerHour = 1_000;
    private const int ChallengeRetentionDaysAfterCreation = 2;
    private const int ChallengeRateLimitAdvisoryLockKey = 30_304;
    private const string RegistrationPurposeName = "Registration";
    private const string PasswordRecoveryPurposeName = "PasswordRecovery";
    private readonly PostgresDbContext dbContext;
    private readonly IEmailChallengeDelivery delivery;
    private readonly IEmailReceiptTokenIssuer receiptIssuer;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlyDictionary<string, byte[]> hmacKeysById;
    private readonly string activeHmacKeyId;

    public PostgresEmailChallengeUseCase(
        PostgresDbContext dbContext,
        IEmailChallengeDelivery delivery,
        IEmailReceiptTokenIssuer receiptIssuer,
        TimeProvider timeProvider,
        IReadOnlyDictionary<string, string> hmacKeysById,
        string activeHmacKeyId)
    {
        this.dbContext = dbContext;
        this.delivery = delivery;
        this.receiptIssuer = receiptIssuer;
        this.timeProvider = timeProvider;
        this.hmacKeysById = hmacKeysById.ToDictionary(pair => pair.Key,
            pair => Encoding.UTF8.GetBytes(pair.Value), StringComparer.Ordinal);
        this.activeHmacKeyId = activeHmacKeyId;
        if (!this.hmacKeysById.TryGetValue(activeHmacKeyId, out _) ||
            this.hmacKeysById.Values.Any(key => key.Length < 32))
            throw new InvalidOperationException("Email challenge HMAC keys must be configured with IDs and at least 32 UTF-8 bytes each.");
    }

    public Task<EmailChallengeOperationResult> StartAsync(StartEmailChallengeRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryGetChallengeInputs(request.Email, request.Purpose, out var normalizedEmail))
            return Task.FromResult(new EmailChallengeOperationResult(EmailChallengeStatus.InvalidRequest));
        if (request.Purpose == RegistrationPurpose && !IsValidPrivacyConsent(request.PrivacyConsent))
            return Task.FromResult(new EmailChallengeOperationResult(EmailChallengeStatus.InvalidRequest));
        return CreateAndSendAsync(normalizedEmail, request.Purpose, request.PrivacyConsent, cancellationToken);
    }

    public async Task<EmailChallengeOperationResult> ResendAsync(
        ResendEmailChallengeRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !TryGetChallengeInputs(request.Email, request.Purpose, out var normalizedEmail) ||
            !Guid.TryParse(request.ChallengeId, out var challengeId))
            return new EmailChallengeOperationResult(EmailChallengeStatus.InvalidRequest);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var preparation = await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => PrepareResendAsync(normalizedEmail, request.Purpose, challengeId, now,
                cancellationToken))
            .ConfigureAwait(false);
        if (preparation.Status != EmailChallengeStatus.Succeeded)
            return new EmailChallengeOperationResult(preparation.Status);

        var newChallengeId = preparation.ChallengeId;
        var code = preparation.Code!;
        bool delivered;
        try
        {
            delivered = await delivery.SendAsync(normalizedEmail, code, request.Purpose, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkSupersededAsync(newChallengeId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        if (!delivered)
        {
            await MarkSupersededAsync(newChallengeId, cancellationToken).ConfigureAwait(false);
            return new EmailChallengeOperationResult(EmailChallengeStatus.DeliveryUnavailable);
        }

        await dbContext.EmailChallengeSessions
            .Where(item => item.Id == challengeId && item.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SupersededAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        return CreateStartResult(newChallengeId);
    }

    private async Task<(EmailChallengeStatus Status, Guid ChallengeId, string? Code)> PrepareResendAsync(
        string normalizedEmail,
        int purpose,
        Guid challengeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Guid newChallengeId;
        string code;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await AcquireRateLimitLockAsync(cancellationToken).ConfigureAwait(false);
            await CleanupExpiredChallengesAsync(now, cancellationToken).ConfigureAwait(false);
            var previous = await dbContext.EmailChallengeSessions
                .FromSqlInterpolated($"SELECT * FROM app.email_challenge_sessions WHERE id = {challengeId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (previous is null || previous.NormalizedEmail != normalizedEmail || previous.Purpose != purpose ||
                previous.ExpiresAtUtc <= now || previous.ConsumedAtUtc is not null || previous.SupersededAtUtc is not null ||
                previous.LockedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (EmailChallengeStatus.InvalidOrExpired, Guid.Empty, null);
            }
            if (previous.ResendAvailableAtUtc > now)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (EmailChallengeStatus.Cooldown, Guid.Empty, null);
            }
            var limit = await CheckRateLimitsAsync(normalizedEmail, now, cancellationToken).ConfigureAwait(false);
            if (limit != EmailChallengeStatus.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (limit, Guid.Empty, null);
            }

            (newChallengeId, code) = CreateChallenge(normalizedEmail, purpose, now);
            var previousConsent = purpose == RegistrationPurpose
                ? await dbContext.EmailChallengeSessions.AsNoTracking()
                    .Where(item => item.Id == challengeId && item.Purpose == RegistrationPurpose)
                    .Select(item => new { item.PrivacyNoticeVersion, item.PrivacyNoticeAcceptedAtUtc })
                    .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                : null;
            dbContext.EmailChallengeSessions.Add(new EmailChallengeSessionRecord
            {
                Id = newChallengeId, NormalizedEmail = normalizedEmail, Purpose = purpose,
                HmacKeyId = activeHmacKeyId,
                CodeMac = ComputeCodeMac(activeHmacKeyId, normalizedEmail, purpose, newChallengeId, code),
                CreatedAtUtc = now,
                ExpiresAtUtc = timeProvider.GetUtcNow().AddMinutes(CodeLifetimeMinutes).UtcDateTime,
                ResendAvailableAtUtc = timeProvider.GetUtcNow().AddSeconds(ResendCooldownSeconds).UtcDateTime,
                PrivacyNoticeVersion = previousConsent?.PrivacyNoticeVersion,
                PrivacyNoticeAcceptedAtUtc = previousConsent?.PrivacyNoticeAcceptedAtUtc
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return (EmailChallengeStatus.Succeeded, newChallengeId, code);
    }

    public async Task<EmailChallengeOperationResult> VerifyAsync(
        VerifyEmailChallengeRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !TryGetChallengeInputs(request.Email, request.Purpose, out var normalizedEmail) ||
            !Guid.TryParse(request.ChallengeId, out var challengeId) ||
            !AuthInputValidator.TryValidateOtpCode(request.Code, out var code, out _))
            return new EmailChallengeOperationResult(EmailChallengeStatus.InvalidRequest);

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => VerifyTransactionalAsync(request.Purpose, normalizedEmail, challengeId, code,
                cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<EmailChallengeOperationResult> VerifyTransactionalAsync(
        int purpose,
        string normalizedEmail,
        Guid challengeId,
        string code,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        var challenge = await dbContext.EmailChallengeSessions
            .FromSqlInterpolated($"SELECT * FROM app.email_challenge_sessions WHERE id = {challengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (challenge is null || challenge.NormalizedEmail != normalizedEmail || challenge.Purpose != purpose ||
            challenge.ExpiresAtUtc <= now.UtcDateTime || challenge.ConsumedAtUtc is not null ||
            challenge.SupersededAtUtc is not null || challenge.LockedAtUtc is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new EmailChallengeOperationResult(EmailChallengeStatus.InvalidOrExpired);
        }

        if (!hmacKeysById.ContainsKey(challenge.HmacKeyId))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new EmailChallengeOperationResult(EmailChallengeStatus.InvalidOrExpired);
        }
        var suppliedMac = Convert.FromHexString(ComputeCodeMac(challenge.HmacKeyId, normalizedEmail, purpose, challengeId, code));
        var expectedMac = Convert.FromHexString(challenge.CodeMac);
        if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= MaximumChallengeAttempts)
                challenge.LockedAtUtc = now.UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new EmailChallengeOperationResult(EmailChallengeStatus.InvalidOrExpired);
        }

        var receiptId = Guid.CreateVersion7().ToString("N");
        var expiresAt = now.Add(EmailReceiptLifetimePolicy.ForPurpose(purpose));
        var token = receiptIssuer.Issue(normalizedEmail, purpose, challengeId.ToString("D"), receiptId, expiresAt);
        challenge.ConsumedAtUtc = now.UtcDateTime;
        challenge.ReceiptId = receiptId;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new EmailChallengeOperationResult(EmailChallengeStatus.Succeeded,
            VerificationToken: token, ReceiptExpiresInSeconds: checked((int)(expiresAt - now).TotalSeconds));
    }

    private async Task<EmailChallengeOperationResult> CreateAndSendAsync(
        string normalizedEmail, int purpose, MuscleCuties.UseCases.Features.Account.Registration.Contracts.RegistrationPrivacyConsent? privacyConsent, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var preparation = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await AcquireRateLimitLockAsync(cancellationToken).ConfigureAwait(false);
            await CleanupExpiredChallengesAsync(now, cancellationToken).ConfigureAwait(false);
            var limit = await CheckRateLimitsAsync(normalizedEmail, now, cancellationToken).ConfigureAwait(false);
            if (limit != EmailChallengeStatus.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (Status: limit, ChallengeId: Guid.Empty, Code: (string?)null);
            }
            var created = CreateChallenge(normalizedEmail, purpose, now);
            dbContext.EmailChallengeSessions.Add(new EmailChallengeSessionRecord
            {
                Id = created.ChallengeId, NormalizedEmail = normalizedEmail, Purpose = purpose,
                HmacKeyId = activeHmacKeyId,
                CodeMac = ComputeCodeMac(activeHmacKeyId, normalizedEmail, purpose, created.ChallengeId, created.Code), CreatedAtUtc = now,
                ExpiresAtUtc = timeProvider.GetUtcNow().AddMinutes(CodeLifetimeMinutes).UtcDateTime,
                ResendAvailableAtUtc = timeProvider.GetUtcNow().AddSeconds(ResendCooldownSeconds).UtcDateTime,
                PrivacyNoticeVersion = privacyConsent?.NoticeVersion,
                PrivacyNoticeAcceptedAtUtc = privacyConsent is null ? null : now
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (Status: EmailChallengeStatus.Succeeded, created.ChallengeId, Code: (string?)created.Code);
        }).ConfigureAwait(false);
        if (preparation.Status != EmailChallengeStatus.Succeeded)
            return new EmailChallengeOperationResult(preparation.Status);

        var challengeId = preparation.ChallengeId;
        var code = preparation.Code!;

        bool delivered;
        try
        {
            delivered = await delivery.SendAsync(normalizedEmail, code, purpose, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkSupersededAsync(challengeId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        if (!delivered)
        {
            await MarkSupersededAsync(challengeId, cancellationToken).ConfigureAwait(false);
            return new EmailChallengeOperationResult(EmailChallengeStatus.DeliveryUnavailable);
        }
        return CreateStartResult(challengeId);
    }

    private bool IsValidPrivacyConsent(
        MuscleCuties.UseCases.Features.Account.Registration.Contracts.RegistrationPrivacyConsent? consent) =>
        consent is not null && consent.Accepted &&
        consent.NoticeVersion == MuscleCuties.UseCases.Features.Account.Registration.Contracts.PrivacyNotice.CurrentVersion;

    private async Task<EmailChallengeStatus> CheckRateLimitsAsync(
        string normalizedEmail, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var emailCount = await dbContext.EmailChallengeSessions.AsNoTracking()
            .CountAsync(item => item.NormalizedEmail == normalizedEmail &&
                item.CreatedAtUtc > nowUtc.AddDays(-1), cancellationToken).ConfigureAwait(false);
        if (emailCount >= MaximumChallengesPerEmailPerDay)
            return EmailChallengeStatus.RateLimited;
        var globalCount = await dbContext.EmailChallengeSessions.AsNoTracking()
            .CountAsync(item => item.CreatedAtUtc > nowUtc.AddHours(-1), cancellationToken).ConfigureAwait(false);
        return globalCount >= MaximumChallengesGloballyPerHour
            ? EmailChallengeStatus.RateLimited
            : EmailChallengeStatus.Succeeded;
    }

    private Task<int> CleanupExpiredChallengesAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        dbContext.EmailChallengeSessions
            .Where(item => item.CreatedAtUtc < nowUtc.AddDays(-ChallengeRetentionDaysAfterCreation))
            .ExecuteDeleteAsync(cancellationToken);

    private Task<int> AcquireRateLimitLockAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({ChallengeRateLimitAdvisoryLockKey})", cancellationToken);

    private async Task MarkSupersededAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.EmailChallengeSessions.Where(item => item.Id == challengeId && item.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.SupersededAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    private (Guid ChallengeId, string Code) CreateChallenge(string email, int purpose, DateTime nowUtc)
    {
        var value = RandomNumberGenerator.GetInt32(0, MaximumOtpCodeValueExclusive);
        return (Guid.CreateVersion7(), value.ToString($"D{OtpCodeDigits}", CultureInfo.InvariantCulture));
    }

    private string ComputeCodeMac(string keyId, string normalizedEmail, int purpose, Guid challengeId, string code)
    {
        using var hmac = new HMACSHA256(hmacKeysById[keyId]);
        var payload = Encoding.UTF8.GetBytes($"{normalizedEmail}\n{purpose}\n{challengeId:D}\n{code}");
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private EmailChallengeOperationResult CreateStartResult(Guid challengeId) =>
        new(EmailChallengeStatus.Succeeded, challengeId.ToString("D"), ResendCooldownSeconds,
            checked((int)TimeSpan.FromMinutes(CodeLifetimeMinutes).TotalSeconds));

    private static bool TryGetChallengeInputs(string? email, int purpose, out string normalizedEmail)
    {
        normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        return AuthInputValidator.IsValidEmail(normalizedEmail) && purpose is RegistrationPurpose or PasswordRecoveryPurpose;
    }
}
