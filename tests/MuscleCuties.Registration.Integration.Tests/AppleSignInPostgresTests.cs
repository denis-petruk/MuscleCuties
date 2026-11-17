using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Authentication;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[Collection(DisposableDatabaseCollection.Name)]
public sealed class AppleSignInPostgresTests(DisposableDatabaseFixture fixture)
{
    private const string AppleAudience = "com.musclecuties.app";
    private const string AppleKeyId = "syntheticAppleKey";
    private const string SigningKey = "synthetic-apple-signin-key-for-tests-only";
    private const string Issuer = "SyntheticAppleServer";
    private const string Audience = "SyntheticAppleClient";

    [PostgresFact]
    public async Task FirstAppleSignInCreatesAnAccountAndReturnsASession()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_first");
        try
        {
            using var keys = new SyntheticAppleKey();
            await using var db = CreateDbContext(connection);
            var authentication = CreateAuthentication(db, keys);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "new.apple.member@example.invalid", "synthetic-apple-subject-1");

            var result = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId), default);

            Assert.Equal(SessionOperationStatus.Succeeded, result.Status);
            Assert.NotNull(result.Tokens);
            await using var verification = CreateDbContext(connection);
            var user = await verification.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == result.Tokens!.UserId);
            Assert.Equal("synthetic-apple-subject-1", user.AppleUserId);
            Assert.Equal("Apple", user.CredentialType);
            Assert.Equal("new.apple.member@example.invalid", user.Email);
            Assert.True(user.IsOnboardingComplete);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task AppleSignInLinksAnExistingVerifiedEmailAccountInsteadOfCreatingAnother()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_link");
        try
        {
            using var keys = new SyntheticAppleKey();
            var existingUserId = Guid.CreateVersion7();
            await using (var seed = CreateDbContext(connection))
            {
                seed.Users.Add(new UserRecord
                {
                    Id = existingUserId,
                    Email = "linked.member@example.invalid",
                    Username = "linked_member",
                    CredentialType = "Password",
                    PasswordHash = "synthetic-password-hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsOnboardingComplete = true
                });
                await seed.SaveChangesAsync();
            }

            await using var db = CreateDbContext(connection);
            var authentication = CreateAuthentication(db, keys);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "linked.member@example.invalid", "synthetic-apple-subject-2");

            var result = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId), default);

            Assert.Equal(SessionOperationStatus.Succeeded, result.Status);
            Assert.Equal(existingUserId, result.Tokens!.UserId);
            await using var verification = CreateDbContext(connection);
            var user = await verification.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == existingUserId);
            Assert.Equal("synthetic-apple-subject-2", user.AppleUserId);
            Assert.Equal("Password", user.CredentialType);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    private static PostgresDbContext CreateDbContext(string connection)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        return new PostgresDbContext(options);
    }

    private static PostgresAccountAuthenticationUseCase CreateAuthentication(
        PostgresDbContext db,
        SyntheticAppleKey keys) =>
        new(db,
            new Pbkdf2RegistrationPasswordHasher(),
            TimeProvider.System,
            SigningKey,
            Issuer,
            Audience,
            AppleAudience,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AppleKeyId] = keys.PublicKeyPem
            });

    private sealed class SyntheticAppleKey : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public string PublicKeyPem => _rsa.ExportSubjectPublicKeyInfoPem();

        public string CreateToken(string rawNonce, string email, string subject)
        {
            var now = DateTimeOffset.UtcNow;
            var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = AppleKeyId }));
            var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
            {
                ["iss"] = "https://appleid.apple.com",
                ["aud"] = AppleAudience,
                ["sub"] = subject,
                ["nonce"] = AppleIdentityTokenValidator.Hash(rawNonce),
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
                ["email"] = email
            }));
            var signature = _rsa.SignData(
                Encoding.ASCII.GetBytes($"{header}.{payload}"),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return $"{header}.{payload}.{Encode(signature)}";
        }

        public void Dispose() => _rsa.Dispose();

        private static string Encode(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
