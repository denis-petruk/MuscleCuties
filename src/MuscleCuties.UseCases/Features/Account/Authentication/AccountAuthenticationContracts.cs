namespace MuscleCuties.UseCases.Features.Account.Authentication;

/// <summary>An address and password presented for sign-in.</summary>
public sealed record PasswordLoginRequest(string EmailOrUsername, string Password);
/// <summary>An Apple identity token presented for sign-in, with the nonce it was signed with.</summary>
public sealed record AppleLoginRequest(string IdentityToken, string Nonce, string NonceChallengeId, string? AuthorizationCode = null);
/// <summary>A refresh token presented to be exchanged for a new pair.</summary>
public sealed record RefreshSessionRequest(string RefreshToken);
/// <summary>A new password presented under a verified recovery session.</summary>
public sealed record ResetPasswordRequest(string Email, string ChallengeId, string VerificationToken, string NewPassword);
/// <summary>A request to end one session family and its access token.</summary>
public sealed record RevokeSessionRequest(Guid UserId, string RefreshToken, string? AccessTokenId, DateTimeOffset? AccessTokenExpiresAtUtc);
/// <summary>The pair a session is held as: the access token and the refresh token that replaces it.</summary>
public sealed record SessionTokens(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessExpiresAtUtc,
    bool IsOnboardingComplete = true);

/// <summary>How a session operation ended, so the client can tell a refusal from an expiry.</summary>
public enum SessionOperationStatus
{
    Succeeded,
    InvalidCredentials,
    InvalidAppleIdentity,
    InvalidRefreshToken,
    Failed
}

/// <summary>The outcome of a session operation, with the tokens when it succeeded.</summary>
public sealed record SessionOperationResult(SessionOperationStatus Status, SessionTokens? Tokens = null);

/// <summary>Why a password change was refused, or that it succeeded.</summary>
public enum PasswordChangeStatus
{
    Succeeded,
    InvalidRequest,
    InvalidCurrentPassword,
    AccountNotFound
}

/// <summary>The authentication operations the endpoints expose.</summary>
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

/// <summary>Why a password reset was refused, or that it succeeded.</summary>
public enum PasswordResetStatus
{
    Succeeded,
    InvalidRequest,
    InvalidVerification,
    AccountNotFound
}

/// <summary>The reset flow: what the use case offers after a recovery session is verified.</summary>
public interface IAccountPasswordResetUseCase
{
    Task<PasswordResetStatus> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

/// <summary>The persistence the password reset needs, so the use case holds no SQL.</summary>
public interface IAccountPasswordResetStore
{
    Task<PasswordResetStatus> ResetAsync(
        string normalizedEmail,
        string challengeId,
        string receiptId,
        string passwordHash,
        CancellationToken cancellationToken);
}
