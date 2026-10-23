using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.MigrationClaims;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.MigrationClaims;

public sealed class PostgresAccountMigrationClaimStore(PostgresDbContext dbContext, TimeProvider timeProvider)
    : IAccountMigrationClaimStore
{
    public async Task<string> CreateAsync(
        Guid destinationUserId,
        string installationKeyHash,
        string sourceAccountKeyHash,
        string nonceHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.SourceAccountClaimNonces
            .Where(nonce => nonce.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime && nonce.RedeemedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        dbContext.SourceAccountClaimNonces.Add(new SourceAccountClaimNonceRecord
        {
            Id = id,
            DestinationUserId = destinationUserId,
            SourceInstallationKeyHash = installationKeyHash,
            SourceAccountKeyHash = sourceAccountKeyHash,
            NonceHash = nonceHash,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAtUtc = expiresAtUtc.UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return id.ToString("D");
    }

    public async Task<MigrationClaimOutcome> RedeemAsync(
        Guid destinationUserId,
        string claimId,
        string installationKeyHash,
        string sourceAccountKeyHash,
        string nonceHash,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(claimId, out var parsedClaimId))
            return new MigrationClaimOutcome(false, false, null);
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => RedeemTransactionalAsync(destinationUserId, parsedClaimId, installationKeyHash,
                sourceAccountKeyHash, nonceHash, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<MigrationClaimOutcome> RedeemTransactionalAsync(
        Guid destinationUserId,
        Guid parsedClaimId,
        string installationKeyHash,
        string sourceAccountKeyHash,
        string nonceHash,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var claim = await dbContext.SourceAccountClaimNonces
            .FromSqlInterpolated($"SELECT * FROM app.source_account_claim_nonces WHERE id = {parsedClaimId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (claim is null || claim.DestinationUserId != destinationUserId ||
            claim.SourceInstallationKeyHash != installationKeyHash || claim.SourceAccountKeyHash != sourceAccountKeyHash ||
            claim.NonceHash != nonceHash)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new MigrationClaimOutcome(false, false, null);
        }

        if (claim.RedeemedAtUtc is not null)
        {
            var priorMapping = await FindByClaimAsync(destinationUserId, parsedClaimId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return priorMapping is null
                ? new MigrationClaimOutcome(false, false, null)
                : new MigrationClaimOutcome(true, true, priorMapping.Id);
        }
        if (claim.ExpiresAtUtc <= now)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new MigrationClaimOutcome(false, false, null);
        }

        var existingMapping = await dbContext.SourceAccountMappings.AsNoTracking().SingleOrDefaultAsync(mapping =>
                mapping.SourceInstallationKeyHash == installationKeyHash &&
                mapping.SourceAccountKeyHash == sourceAccountKeyHash, cancellationToken)
            .ConfigureAwait(false);
        if (existingMapping is not null)
        {
            claim.RedeemedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var alreadyOwned = existingMapping.DestinationUserId == destinationUserId;
            return new MigrationClaimOutcome(alreadyOwned, alreadyOwned, alreadyOwned ? existingMapping.Id : null);
        }

        var mappingId = Guid.CreateVersion7();
        claim.RedeemedAtUtc = now;
        dbContext.SourceAccountMappings.Add(new SourceAccountMappingRecord
        {
            Id = mappingId,
            ClaimId = parsedClaimId,
            DestinationUserId = destinationUserId,
            SourceInstallationKeyHash = installationKeyHash,
            SourceAccountKeyHash = sourceAccountKeyHash,
            CreatedAtUtc = now
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new MigrationClaimOutcome(true, false, mappingId);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            var racedMapping = await dbContext.SourceAccountMappings.AsNoTracking().SingleOrDefaultAsync(mapping =>
                    mapping.SourceInstallationKeyHash == installationKeyHash &&
                    mapping.SourceAccountKeyHash == sourceAccountKeyHash, cancellationToken)
                .ConfigureAwait(false);
            var alreadyOwned = racedMapping?.DestinationUserId == destinationUserId;
            return new MigrationClaimOutcome(alreadyOwned, alreadyOwned, alreadyOwned ? racedMapping!.Id : null);
        }
    }

    private Task<SourceAccountMappingRecord?> FindByClaimAsync(
        Guid destinationUserId,
        Guid claimId,
        CancellationToken cancellationToken) => dbContext.SourceAccountMappings.AsNoTracking()
        .SingleOrDefaultAsync(mapping => mapping.ClaimId == claimId && mapping.DestinationUserId == destinationUserId,
            cancellationToken);
}
