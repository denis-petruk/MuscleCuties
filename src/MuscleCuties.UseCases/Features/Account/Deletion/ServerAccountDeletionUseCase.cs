using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Deletion;

/// <summary>The server-side deletion: purge the rows, revoke the Apple tokens, record the outcome.</summary>
public sealed class ServerAccountDeletionUseCase(
    IServerAccountPurgeStore purgeStore,
    IAccountDeletionAuditStore? auditStore = null,
    IAppleAccountTokenRevoker? appleTokenRevoker = null)
{
    /// <summary>Deletes the account and everything it owns for the calling user.</summary>
    public async Task<AccountDeletionResult> DeleteAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        string? appleAuthorizationCode = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (appleTokenRevoker is not null)
        {
            var revocation = await appleTokenRevoker
                .RevokeAccountTokenAsync(userId, appleAuthorizationCode, cancellationToken)
                .ConfigureAwait(false);
            if (!revocation.Succeeded)
            {
                return new AccountDeletionResult(
                    Succeeded: false,
                    PurgedRecordCount: 0,
                    CleanedMediaKeys: [],
                    Message: revocation.FailureReason ?? "Apple Sign-In token revocation failed; account was not purged.",
                    FailureCode: revocation.FailureCode);
            }
        }

        var result = await purgeStore.PurgeAccountDataAsync(userId, cancellationToken).ConfigureAwait(false);

        if (result.Succeeded && auditStore != null)
        {
            var tombstone = new AccountDeletionAuditEntry(
                TombstoneId: Guid.NewGuid(),
                UserId: userId,
                PurgedAtUtc: DateTime.UtcNow,
                Reason: "UserRequested",
                PurgedRecordCount: result.PurgedRecordCount,
                CleanedMediaKeys: result.CleanedMediaKeys);

            await auditStore.RecordDeletionAuditAsync(tombstone, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
