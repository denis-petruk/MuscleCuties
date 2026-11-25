using System.Security.Cryptography;
using System.Text;

namespace MuscleCuties.UseCases.Features.Account.MigrationClaims;

/// <summary>Issues and redeems claims, refusing a source account that is already mapped.</summary>
public sealed class AccountMigrationClaimUseCase(
    IAccountMigrationClaimStore store,
    TimeProvider timeProvider) : IAccountMigrationClaimUseCase
{
    private const int MinimumSourceKeyCharacters = 43;
    private const int MaximumSourceKeyCharacters = 128;
    private const int ClaimLifetimeMinutes = 10;
    private const int NonceLengthBytes = 32;

    /// <summary>Creates a single-use claim that lets a legacy account be migrated.</summary>
    public async Task<CreatedMigrationClaim?> CreateAsync(
        Guid destinationUserId,
        CreateMigrationClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (destinationUserId == Guid.Empty || request is null || !IsValidSourceKey(request.SourceInstallationKey) ||
            !IsValidSourceKey(request.SourceAccountKey))
            return null;
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceLengthBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return await CreateWithNonceAsync(destinationUserId, request, nonce, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Redeems a migration claim, reporting whether it was used, expired or unknown.</summary>
    public Task<MigrationClaimOutcome> RedeemAsync(
        Guid destinationUserId,
        RedeemMigrationClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (destinationUserId == Guid.Empty || request is null || !Guid.TryParse(request.ClaimId, out _) ||
            !IsValidSourceKey(request.SourceInstallationKey) || !IsValidSourceKey(request.SourceAccountKey) ||
            string.IsNullOrWhiteSpace(request.Nonce))
            return Task.FromResult(new MigrationClaimOutcome(false, false, null));
        return store.RedeemAsync(
            destinationUserId,
            request.ClaimId,
            Hash(request.SourceInstallationKey),
            Hash(request.SourceAccountKey),
            Hash(request.Nonce),
            cancellationToken);
    }

    /// <summary>Creates the claim with a fresh nonce, retrying until the nonce is unique.</summary>
    private async Task<CreatedMigrationClaim> CreateWithNonceAsync(
        Guid destinationUserId,
        CreateMigrationClaimRequest request,
        string nonce,
        CancellationToken cancellationToken)
    {
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(ClaimLifetimeMinutes);
        var claimId = await store.CreateAsync(
            destinationUserId,
            Hash(request.SourceInstallationKey),
            Hash(request.SourceAccountKey),
            Hash(nonce),
            expiresAt,
            cancellationToken).ConfigureAwait(false);
        return new CreatedMigrationClaim(claimId, nonce, expiresAt);
    }

    /// <summary>True when the source key is a plausible external account identifier.</summary>
    private static bool IsValidSourceKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length is >= MinimumSourceKeyCharacters and <= MaximumSourceKeyCharacters &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    /// <summary>SHA-256 hash, hex encoded, so a claim is stored without its raw key.</summary>
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
