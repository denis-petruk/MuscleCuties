using System.Security.Cryptography;
using System.Text;

namespace MuscleCuties.UseCases.Features.Account.MigrationClaims;

public sealed class AccountMigrationClaimUseCase(
    IAccountMigrationClaimStore store,
    TimeProvider timeProvider) : IAccountMigrationClaimUseCase
{
    private const int MinimumSourceKeyCharacters = 43;
    private const int MaximumSourceKeyCharacters = 128;
    private const int ClaimLifetimeMinutes = 10;
    private const int NonceLengthBytes = 32;

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

    private static bool IsValidSourceKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length is >= MinimumSourceKeyCharacters and <= MaximumSourceKeyCharacters &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
