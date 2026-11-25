namespace MuscleCuties.UseCases.Features.Account.MigrationClaims;

/// <summary>A request to claim a pre-existing account for the signed-in one.</summary>
public sealed record CreateMigrationClaimRequest(string SourceInstallationKey, string SourceAccountKey);
/// <summary>The nonce and source identifiers presented to complete a claim.</summary>
public sealed record RedeemMigrationClaimRequest(
    string ClaimId,
    string SourceInstallationKey,
    string SourceAccountKey,
    string Nonce);
/// <summary>A claim that was issued, with the nonce the client must redeem.</summary>
public sealed record CreatedMigrationClaim(string ClaimId, string Nonce, DateTimeOffset ExpiresAtUtc);
/// <summary>How a claim ended, so the client can tell a bad nonce from an already-claimed account.</summary>
public sealed record MigrationClaimOutcome(bool Succeeded, bool AlreadyLinked, Guid? MappingId);

/// <summary>Issues and redeems account migration claims.</summary>
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

/// <summary>The persistence a migration claim needs.</summary>
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
