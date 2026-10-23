using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Registration;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;

public sealed class PostgresAccountAuthenticationUseCase : IAccountAuthenticationUseCase
{
    private const int AccessLifetimeMinutes = 15;
    private const int RefreshLifetimeDays = 30;
    private const int AppleNonceLifetimeMinutes = 5;
    private const int ExpiredSessionRetentionDays = 1;
    private const int MaximumIdentityTokenCharacters = 16_384;
    private const int MaximumLoginIdentifierCharacters = 320;
    private const int MinimumAppleRsaKeyBits = 2_048;
    private const long MaximumAppleIdentityLifetimeSeconds = 3_600;
    private const long AllowedAppleIssueTimeFutureSeconds = 60;
    private readonly PostgresDbContext dbContext;
    private readonly IRegistrationPasswordHasher passwordHasher;
    private readonly PostgresPasswordLoginLimiter passwordLoginLimiter;
    private readonly TimeProvider timeProvider;
    private readonly byte[] accessSigningKey;
    private readonly string issuer;
    private readonly string audience;
    private readonly string appleAudience;
    private readonly IReadOnlyDictionary<string, string> applePublicKeysById;
    private readonly IAppleAccountTokenRevoker? appleTokenRevoker;

    public PostgresAccountAuthenticationUseCase(
        PostgresDbContext dbContext,
        IRegistrationPasswordHasher passwordHasher,
        TimeProvider timeProvider,
        string accessSigningKey,
        string issuer,
        string audience,
        string appleAudience,
        IReadOnlyDictionary<string, string> applePublicKeysById,
        IAppleAccountTokenRevoker? appleTokenRevoker = null)
    {
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.timeProvider = timeProvider;
        this.accessSigningKey = Encoding.UTF8.GetBytes(accessSigningKey);
        passwordLoginLimiter = new PostgresPasswordLoginLimiter(dbContext, timeProvider, this.accessSigningKey);
        this.issuer = issuer;
        this.audience = audience;
        this.appleAudience = appleAudience;
        this.applePublicKeysById = applePublicKeysById;
        this.appleTokenRevoker = appleTokenRevoker;
    }

    public async Task<(string ChallengeId, string Nonce)> CreateAppleNonceAsync(CancellationToken cancellationToken)
    {
        await dbContext.AppleSignInNonces.Where(item => item.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var nonce = Encode(RandomNumberGenerator.GetBytes(32));
        var nonceHash = Hash(nonce);
        var record = new AppleSignInNonceRecord
        {
            Id = Guid.CreateVersion7(),
            NonceHash = nonceHash,
            ExpiresAtUtc = timeProvider.GetUtcNow().AddMinutes(AppleNonceLifetimeMinutes).UtcDateTime
        };
        dbContext.AppleSignInNonces.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (record.Id.ToString("D"), nonce);
    }

    public async Task<SessionOperationResult> LoginWithPasswordAsync(
        PasswordLoginRequest request,
        string? trustedSourceAddress,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.EmailOrUsername) ||
            request.EmailOrUsername.Length > MaximumLoginIdentifierCharacters || string.IsNullOrEmpty(request.Password))
            return new SessionOperationResult(SessionOperationStatus.InvalidCredentials);

        var normalizedIdentifier = request.EmailOrUsername.Trim().ToLowerInvariant();
        if (!await passwordLoginLimiter.ReserveAsync(normalizedIdentifier, trustedSourceAddress, cancellationToken)
                .ConfigureAwait(false))
            return new SessionOperationResult(SessionOperationStatus.InvalidCredentials);

        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedIdentifier ||
                (candidate.Username != null && candidate.Username.Trim().ToLower() == normalizedIdentifier), cancellationToken)
            .ConfigureAwait(false);
        if (user is not null && !await passwordLoginLimiter.ReserveCanonicalAccountAsync(user.Id, cancellationToken)
                .ConfigureAwait(false))
            return new SessionOperationResult(SessionOperationStatus.InvalidCredentials);
        var passwordVerified = passwordHasher.VerifyPassword(request.Password, user?.PasswordHash ?? string.Empty);
        if (user is null || !passwordVerified)
            return new SessionOperationResult(SessionOperationStatus.InvalidCredentials);

        var familyId = Guid.CreateVersion7();
        var result = await CreateSessionAsync(user.Id, familyId, cancellationToken).ConfigureAwait(false);
        await passwordLoginLimiter.ClearAccountAfterSuccessAsync(user, normalizedIdentifier, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<SessionOperationResult> LoginWithAppleAsync(
        AppleLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !TryVerifyAppleIdentityToken(request.IdentityToken, request.Nonce, out var appleUserId))
            return new SessionOperationResult(SessionOperationStatus.InvalidAppleIdentity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var nonceId = Guid.TryParse(request.NonceChallengeId, out var parsedId) ? parsedId : Guid.Empty;
        var nonce = await dbContext.AppleSignInNonces.SingleOrDefaultAsync(
            item => item.Id == nonceId && item.NonceHash == Hash(request.Nonce), cancellationToken).ConfigureAwait(false);
        if (nonce is null || nonce.ExpiresAtUtc <= now || nonce.RedeemedAtUtc is not null)
            return new SessionOperationResult(SessionOperationStatus.InvalidAppleIdentity);

        string? appleRefreshToken = null;
        if (!string.IsNullOrWhiteSpace(request.AuthorizationCode) && appleTokenRevoker is not null)
        {
            appleRefreshToken = await appleTokenRevoker
                .ExchangeAuthorizationCodeForRefreshTokenAsync(request.AuthorizationCode, cancellationToken)
                .ConfigureAwait(false);
        }

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => LoginWithAppleTransactionalAsync(
                nonceId, appleUserId, request.AuthorizationCode, appleRefreshToken, now, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<SessionOperationResult> LoginWithAppleTransactionalAsync(
        Guid nonceId,
        string appleUserId,
        string? authorizationCode,
        string? appleRefreshToken,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var redeemed = await dbContext.AppleSignInNonces
            .Where(item => item.Id == nonceId && item.RedeemedAtUtc == null && item.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.RedeemedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        if (redeemed != 1)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SessionOperationResult(SessionOperationStatus.InvalidAppleIdentity);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.AppleUserId == appleUserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SessionOperationResult(SessionOperationStatus.InvalidAppleIdentity);
        }

        var canStoreAppleToken = string.Equals(user.CredentialType, "Apple", StringComparison.OrdinalIgnoreCase) ||
                                 string.IsNullOrWhiteSpace(user.PasswordHash) ||
                                 user.PasswordHash.StartsWith(AppleAccountTokenRevoker.StoredRefreshTokenPrefix, StringComparison.Ordinal) ||
                                 user.PasswordHash.StartsWith(AppleAccountTokenRevoker.StoredAuthCodePrefix, StringComparison.Ordinal);
        if (canStoreAppleToken)
        {
            if (!string.IsNullOrWhiteSpace(appleRefreshToken))
            {
                user.PasswordHash = $"{AppleAccountTokenRevoker.StoredRefreshTokenPrefix}{appleRefreshToken.Trim()}";
                user.UpdatedAt = now;
            }
            else if (!string.IsNullOrWhiteSpace(authorizationCode) &&
                     (string.IsNullOrWhiteSpace(user.PasswordHash) ||
                      !user.PasswordHash.StartsWith(AppleAccountTokenRevoker.StoredRefreshTokenPrefix, StringComparison.Ordinal)))
            {
                user.PasswordHash = $"{AppleAccountTokenRevoker.StoredAuthCodePrefix}{authorizationCode.Trim()}";
                user.UpdatedAt = now;
            }
        }

        var familyId = Guid.CreateVersion7();
        var result = await CreateSessionAsync(user.Id, familyId, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<SessionOperationResult> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return new SessionOperationResult(SessionOperationStatus.InvalidRefreshToken);
        var tokenHash = Hash(request.RefreshToken);
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => RefreshTransactionalAsync(tokenHash, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<SessionOperationResult> RefreshTransactionalAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var session = await dbContext.RefreshSessions
            .FromSqlInterpolated($"SELECT * FROM app.refresh_sessions WHERE token_hash = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (session is null || session.ExpiresAtUtc <= now || session.RevokedAtUtc is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SessionOperationResult(SessionOperationStatus.InvalidRefreshToken);
        }

        if (session.ReplacedAtUtc is not null)
        {
            await dbContext.RefreshSessions.Where(item => item.FamilyId == session.FamilyId && item.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAtUtc, now), cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SessionOperationResult(SessionOperationStatus.InvalidRefreshToken);
        }

        session.ReplacedAtUtc = now;
        var nextToken = Encode(RandomNumberGenerator.GetBytes(32));
        dbContext.RefreshSessions.Add(new RefreshSessionRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = session.UserId,
            FamilyId = session.FamilyId,
            TokenHash = Hash(nextToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = timeProvider.GetUtcNow().AddDays(RefreshLifetimeDays).UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SessionOperationResult(SessionOperationStatus.Succeeded,
            CreateTokens(session.UserId, session.FamilyId, nextToken, timeProvider.GetUtcNow()));
    }

    public async Task<bool> RevokeAsync(RevokeSessionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.RefreshToken))
            return false;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = Hash(request.RefreshToken);
        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => RevokeTransactionalAsync(request, tokenHash, now, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<bool> RevokeTransactionalAsync(
        RevokeSessionRequest request,
        string tokenHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash && item.UserId == request.UserId && item.RevokedAtUtc == null,
            cancellationToken).ConfigureAwait(false);
        if (session is not null)
            await dbContext.RefreshSessions.Where(item => item.FamilyId == session.FamilyId && item.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAtUtc, now), cancellationToken)
                .ConfigureAwait(false);
        var revokedAccessToken = !string.IsNullOrWhiteSpace(request.AccessTokenId) &&
            await dbContext.RevokedAccessTokens.AnyAsync(item => item.TokenId == request.AccessTokenId, cancellationToken)
                .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.AccessTokenId) &&
            request.AccessTokenId.Length <= 128 && !revokedAccessToken &&
            request.AccessTokenExpiresAtUtc is { } expiration && expiration > timeProvider.GetUtcNow())
        {
            dbContext.RevokedAccessTokens.Add(new RevokedAccessTokenRecord
            {
                Id = Guid.CreateVersion7(), UserId = request.UserId, TokenId = request.AccessTokenId,
                ExpiresAtUtc = expiration.UtcDateTime, RevokedAtUtc = now
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            revokedAccessToken = true;
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return session is not null || revokedAccessToken;
    }

    public async Task<bool> IsAccessTokenRevokedAsync(string tokenId, string sessionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(sessionId, out var familyId))
            return true;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await dbContext.RevokedAccessTokens.AsNoTracking()
                .AnyAsync(token => token.TokenId == tokenId && token.ExpiresAtUtc > now, cancellationToken)
                .ConfigureAwait(false))
            return true;
        return !await dbContext.RefreshSessions.AsNoTracking()
            .AnyAsync(session => session.FamilyId == familyId && session.ExpiresAtUtc > now && session.RevokedAtUtc == null,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<PasswordChangeStatus> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            !MuscleCuties.Core.Domain.Auth.AuthInputValidator.IsStrongPassword(newPassword) ||
            string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            return PasswordChangeStatus.InvalidRequest;

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ChangePasswordTransactionalAsync(userId, currentPassword, newPassword, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<PasswordChangeStatus> ChangePasswordTransactionalAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM app.users WHERE id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PasswordChangeStatus.AccountNotFound;
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PasswordChangeStatus.InvalidCurrentPassword;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwordHasher.HashPassword(newPassword);
        user.CredentialType = "Password";
        user.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return PasswordChangeStatus.Succeeded;
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return Task.FromResult(false);
        var normalized = email.Trim().ToLowerInvariant();
        return dbContext.Users.AsNoTracking().AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username)) return Task.FromResult(false);
        var normalized = username.Trim().ToLowerInvariant();
        return dbContext.Users.AsNoTracking().AnyAsync(u => u.Username != null && u.Username.ToLower() == normalized, cancellationToken);
    }

    private async Task<SessionOperationResult> CreateSessionAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await dbContext.RefreshSessions
            .Where(session => session.ExpiresAtUtc < now.UtcDateTime.AddDays(-ExpiredSessionRetentionDays))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.RevokedAccessTokens
            .Where(token => token.ExpiresAtUtc <= now.UtcDateTime)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var refreshToken = Encode(RandomNumberGenerator.GetBytes(32));
        dbContext.RefreshSessions.Add(new RefreshSessionRecord
        {
            Id = Guid.CreateVersion7(), UserId = userId, FamilyId = familyId,
            TokenHash = Hash(refreshToken), CreatedAtUtc = now.UtcDateTime,
            ExpiresAtUtc = now.AddDays(RefreshLifetimeDays).UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SessionOperationResult(SessionOperationStatus.Succeeded, CreateTokens(userId, familyId, refreshToken, now));
    }

    private SessionTokens CreateTokens(Guid userId, Guid familyId, string refreshToken, DateTimeOffset now)
    {
        var expires = now.AddMinutes(AccessLifetimeMinutes);
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer, aud = audience, sub = userId.ToString("D"),
            iat = now.ToUnixTimeSeconds(), exp = expires.ToUnixTimeSeconds(),
            jti = Guid.CreateVersion7().ToString("D"), sid = familyId.ToString("D")
        }));
        var unsignedToken = $"{header}.{payload}";
        using var hmac = new HMACSHA256(accessSigningKey);
        var accessToken = $"{unsignedToken}.{Encode(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken)))}";
        return new SessionTokens(userId, accessToken, refreshToken, expires);
    }

    private bool TryVerifyAppleIdentityToken(string token, string nonce, out string appleUserId)
    {
        appleUserId = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumIdentityTokenCharacters ||
            string.IsNullOrWhiteSpace(nonce) || applePublicKeysById.Count == 0 || string.IsNullOrWhiteSpace(appleAudience))
            return false;
        var pieces = token.Split('.');
        if (pieces.Length != 3)
            return false;
        try
        {
            using var headerDoc = JsonDocument.Parse(Decode(pieces[0]));
            var header = headerDoc.RootElement;
            if (!header.TryGetProperty("alg", out var alg) || alg.GetString() != "RS256" ||
                !header.TryGetProperty("kid", out var kid) || kid.GetString() is not { } keyId ||
                !applePublicKeysById.TryGetValue(keyId, out var pem))
                return false;
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < MinimumAppleRsaKeyBits || !rsa.VerifyData(
                    Encoding.ASCII.GetBytes($"{pieces[0]}.{pieces[1]}"), Decode(pieces[2]),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return false;
            using var claimsDoc = JsonDocument.Parse(Decode(pieces[1]));
            var claims = claimsDoc.RootElement;
            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            if (!ReadString(claims, "iss", out var tokenIssuer) || tokenIssuer != "https://appleid.apple.com" ||
                !ReadString(claims, "aud", out var tokenAudience) || tokenAudience != appleAudience ||
                !ReadString(claims, "sub", out appleUserId) ||
                !ReadString(claims, "nonce", out var tokenNonce) || tokenNonce != Hash(nonce) ||
                !claims.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var expiresAt) || expiresAt <= now ||
                !claims.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out var issuedAt) ||
                issuedAt > now + AllowedAppleIssueTimeFutureSeconds || expiresAt <= issuedAt ||
                expiresAt - issuedAt > MaximumAppleIdentityLifetimeSeconds)
            {
                appleUserId = string.Empty;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException or ArgumentException)
        {
            appleUserId = string.Empty;
            return false;
        }
    }

    private static bool ReadString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
}
