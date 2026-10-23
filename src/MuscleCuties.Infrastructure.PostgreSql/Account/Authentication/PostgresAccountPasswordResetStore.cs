using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Authentication;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;

public sealed class PostgresAccountPasswordResetStore(PostgresDbContext dbContext, TimeProvider timeProvider)
    : IAccountPasswordResetStore
{
    private const int PasswordRecoveryPurpose = 2;
    private const string PasswordRecoveryPurposeName = "PasswordRecovery";

    public async Task<PasswordResetStatus> ResetAsync(
        string normalizedEmail,
        string challengeId,
        string receiptId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(challengeId, out var parsedChallengeId))
            return PasswordResetStatus.InvalidVerification;
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ResetTransactionalAsync(normalizedEmail, parsedChallengeId, receiptId, passwordHash,
                cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<PasswordResetStatus> ResetTransactionalAsync(
        string normalizedEmail,
        Guid parsedChallengeId,
        string receiptId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var challenge = await dbContext.EmailChallengeSessions
            .FromSqlInterpolated($"SELECT * FROM app.email_challenge_sessions WHERE id = {parsedChallengeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (challenge is null || challenge.NormalizedEmail != normalizedEmail || challenge.Purpose != PasswordRecoveryPurpose ||
            challenge.ConsumedAtUtc is null || challenge.ReceiptId != receiptId || challenge.SupersededAtUtc is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PasswordResetStatus.InvalidVerification;
        }

        var user = await dbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM app.users WHERE normalized_email = {normalizedEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PasswordResetStatus.AccountNotFound;
        }
        if (await dbContext.EmailChallenges.AnyAsync(item => item.Id == receiptId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PasswordResetStatus.InvalidVerification;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwordHash;
        user.CredentialType = "Password";
        user.UpdatedAt = now;
        dbContext.EmailChallenges.Add(new EmailChallengeRecord
        {
            Id = receiptId,
            Purpose = PasswordRecoveryPurposeName,
            RedeemedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.RefreshSessions.Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return PasswordResetStatus.Succeeded;
    }
}
