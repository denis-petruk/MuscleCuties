using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;

namespace MuscleCuties.Infrastructure.Account.Recovery;

public sealed class LegacyAccountRecoveryStore(IServiceScopeFactory scopeFactory) : IAccountRecoveryStore
{
    public async Task<AccountRecoveryStartResult> StartAsync(
        string emailOrUsername, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>()
            .StartRecoveryAsync(emailOrUsername, cancellationToken).ConfigureAwait(false);
        return new AccountRecoveryStartResult(result.Succeeded, result.ChallengeId, result.Email,
            result.MaskedEmail, result.ResendCooldownSeconds, result.ErrorMessage);
    }

    public async Task<AccountRecoveryStartResult> ResendAsync(
        string challengeId, string email, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>()
            .ResendRecoveryCodeAsync(challengeId, email, cancellationToken).ConfigureAwait(false);
        return new AccountRecoveryStartResult(result.Succeeded, result.ChallengeId, result.Email,
            result.MaskedEmail, result.ResendCooldownSeconds, result.ErrorMessage);
    }

    public async Task<AccountRecoveryVerifyResult> VerifyAsync(
        string challengeId, string email, string code, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>()
            .VerifyRecoveryCodeAsync(challengeId, email, code, cancellationToken).ConfigureAwait(false);
        return new AccountRecoveryVerifyResult(result.Succeeded, result.VerificationToken, result.ErrorMessage);
    }

    public async Task<AccountPasswordResetResult> ResetAsync(
        string email, string verificationToken, string newPassword, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>()
            .ResetPasswordAsync(email, verificationToken, newPassword, cancellationToken).ConfigureAwait(false);
        return new AccountPasswordResetResult(result.Succeeded, result.ErrorMessage);
    }

    public void ClearSession(string? email)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().ClearRecoverySession(email);
    }
}
