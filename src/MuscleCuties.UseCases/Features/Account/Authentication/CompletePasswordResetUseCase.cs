using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.UseCases.Features.Account.Registration;

namespace MuscleCuties.UseCases.Features.Account.Authentication;

public sealed class CompletePasswordResetUseCase(
    IRegistrationEmailReceiptVerifier receiptVerifier,
    IRegistrationPasswordHasher passwordHasher,
    IAccountPasswordResetStore store,
    TimeProvider timeProvider) : IAccountPasswordResetUseCase
{
    public async Task<PasswordResetStatus> ExecuteAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !AuthInputValidator.IsValidEmail(request.Email) ||
            !AuthInputValidator.IsStrongPassword(request.NewPassword))
            return PasswordResetStatus.InvalidRequest;
        var normalizedEmail = AuthInputValidator.NormalizeEmail(request.Email);
        if (!receiptVerifier.TryVerify(request.VerificationToken, normalizedEmail, request.ChallengeId, out var receipt) ||
            receipt is null || receipt.ExpiresAt <= timeProvider.GetUtcNow())
            return PasswordResetStatus.InvalidVerification;

        var passwordHash = passwordHasher.HashPassword(request.NewPassword);
        return await store.ResetAsync(normalizedEmail, request.ChallengeId, receipt.Id, passwordHash, cancellationToken)
            .ConfigureAwait(false);
    }
}
