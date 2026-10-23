namespace MuscleCuties.UseCases.Features.Account.MigrationClaims;

public sealed record CreateMigrationClaimRequest(string SourceInstallationKey, string SourceAccountKey);
public sealed record RedeemMigrationClaimRequest(
    string ClaimId,
    string SourceInstallationKey,
    string SourceAccountKey,
    string Nonce);
public sealed record CreatedMigrationClaim(string ClaimId, string Nonce, DateTimeOffset ExpiresAtUtc);
public sealed record MigrationClaimOutcome(bool Succeeded, bool AlreadyLinked, Guid? MappingId);

public interface IAccountMigrationClaimUseCase
{
    Task<CreatedMigrationClaim?> CreateAsync(
        Guid destinationUserId,
        CreateMigrationClaimRequest request,
        CancellationToken cancellationToken);
    Task<MigrationClaimOutcome> RedeemAsync(
        Guid destinationUserId,
        RedeemMigrationClaimRequest request,
        CancellationToken cancellationToken);
}

public interface IAccountMigrationClaimStore
{
    Task<string> CreateAsync(
        Guid destinationUserId,
        string installationKeyHash,
        string sourceAccountKeyHash,
        string nonceHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken);
    Task<MigrationClaimOutcome> RedeemAsync(
        Guid destinationUserId,
        string claimId,
        string installationKeyHash,
        string sourceAccountKeyHash,
        string nonceHash,
        CancellationToken cancellationToken);
}
