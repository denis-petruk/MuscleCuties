namespace MuscleCuties.UseCases.Features.Account.Authentication;

public sealed record PasswordLoginRequest(string EmailOrUsername, string Password);
public sealed record AppleLoginRequest(string IdentityToken, string Nonce, string NonceChallengeId, string? AuthorizationCode = null);
public sealed record RefreshSessionRequest(string RefreshToken);
public sealed record ResetPasswordRequest(string Email, string ChallengeId, string VerificationToken, string NewPassword);
public sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);
public sealed record RevokeSessionRequest(Guid UserId, string RefreshToken, string? AccessTokenId, DateTimeOffset? AccessTokenExpiresAtUtc);
public sealed record SessionTokens(Guid UserId, string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAtUtc);

public enum SessionOperationStatus
{
    Succeeded,
    InvalidCredentials,
    InvalidAppleIdentity,
    InvalidRefreshToken,
    Failed
}

public sealed record SessionOperationResult(SessionOperationStatus Status, SessionTokens? Tokens = null);

public enum PasswordChangeStatus
{
    Succeeded,
    InvalidRequest,
    InvalidCurrentPassword,
    AccountNotFound
}

public interface IAccountAuthenticationUseCase
{
    Task<(string ChallengeId, string Nonce)> CreateAppleNonceAsync(CancellationToken cancellationToken);
    Task<SessionOperationResult> LoginWithPasswordAsync(
        PasswordLoginRequest request, string? trustedSourceAddress, CancellationToken cancellationToken);
    Task<SessionOperationResult> LoginWithAppleAsync(AppleLoginRequest request, CancellationToken cancellationToken);
    Task<SessionOperationResult> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(RevokeSessionRequest request, CancellationToken cancellationToken);
    Task<bool> IsAccessTokenRevokedAsync(string tokenId, string sessionId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    Task<PasswordChangeStatus> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

public enum PasswordResetStatus
{
    Succeeded,
    InvalidRequest,
    InvalidVerification,
    AccountNotFound
}

public interface IAccountPasswordResetUseCase
{
    Task<PasswordResetStatus> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

public interface IAccountPasswordResetStore
{
    Task<PasswordResetStatus> ResetAsync(
        string normalizedEmail,
        string challengeId,
        string receiptId,
        string passwordHash,
        CancellationToken cancellationToken);
}
