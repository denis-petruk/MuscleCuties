using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
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
            Assert.False(user.IsOnboardingComplete);
            Assert.False(result.Tokens!.IsOnboardingComplete);
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

    [PostgresFact]
    public async Task AppleSignInSucceedsAndStoresAuthorizationCodeWhenTokenExchangeThrowsNetworkError()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_exchange_error");
        try
        {
            using var keys = new SyntheticAppleKey();
            await using var db = CreateDbContext(connection);
            var revoker = new FaultingAppleTokenRevoker(() => throw new HttpRequestException("Network is unreachable"));
            var authentication = CreateAuthentication(db, keys, revoker);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "network.fallback@example.invalid", "synthetic-apple-subject-3");

            var result = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId, "synthetic-apple-auth-code"), default);

            Assert.Equal(SessionOperationStatus.Succeeded, result.Status);
            Assert.NotNull(result.Tokens);
            await using var verification = CreateDbContext(connection);
            var user = await verification.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == result.Tokens!.UserId);
            Assert.Equal($"{AppleAccountTokenRevoker.StoredAuthCodePrefix}synthetic-apple-auth-code", user.PasswordHash);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task AppleSignInBoundsTokenExchangeTimeoutAndFallsBackToStoredAuthorizationCode()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_exchange_timeout");
        try
        {
            using var keys = new SyntheticAppleKey();
            await using var db = CreateDbContext(connection);
            var revoker = new FaultingAppleTokenRevoker(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return "unexpected-refresh-token";
            });
            var authentication = CreateAuthentication(db, keys, revoker);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "timeout.fallback@example.invalid", "synthetic-apple-subject-4");

            var startedAt = DateTimeOffset.UtcNow;
            var result = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId, "synthetic-timeout-auth-code"), default);
            var elapsed = DateTimeOffset.UtcNow - startedAt;

            Assert.Equal(SessionOperationStatus.Succeeded, result.Status);
            Assert.NotNull(result.Tokens);
            Assert.True(elapsed < TimeSpan.FromSeconds(10), $"Expected bounded token exchange timeout, elapsed {elapsed}.");
            await using var verification = CreateDbContext(connection);
            var user = await verification.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == result.Tokens!.UserId);
            Assert.Equal($"{AppleAccountTokenRevoker.StoredAuthCodePrefix}synthetic-timeout-auth-code", user.PasswordHash);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task FirstAppleSignInProvisionsInitialProfileSoWorkoutPreferencesCanBeSaved()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_profile_provision");
        try
        {
            using var keys = new SyntheticAppleKey();
            await using var db = CreateDbContext(connection);
            var authentication = CreateAuthentication(db, keys);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "apple.workout@example.invalid", "synthetic-apple-subject-5");

            var authResult = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId), default);

            Assert.Equal(SessionOperationStatus.Succeeded, authResult.Status);
            var userId = authResult.Tokens!.UserId;

            await using var profileDb = CreateDbContext(connection);
            var profileStore = new PostgresProfileDataStore(profileDb, TimeProvider.System);
            var snapshot = await profileStore.LoadAsync(userId, default);

            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot!.Version);
            Assert.Null(snapshot.DateOfBirth);
            Assert.Equal(string.Empty, snapshot.EquipmentLevel);

            var saveResult = await profileStore.UpdateWorkoutPreferencesAsync(
                new UpdateServerWorkoutPreferencesRequest(
                    userId,
                    snapshot.Version,
                    [0],
                    [1, 3, 5],
                    new Dictionary<int, int> { [0] = 45 },
                    "FullGym",
                    new Dictionary<int, int>(),
                    new HashSet<int>(),
                    false),
                default);

            Assert.Equal(ServerProfileWriteStatus.Saved, saveResult.Status);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task AuthenticatedAppleAccountCompletesOnboardingAndPersistsProfileQuizAndCycleAnchor()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_full_onboarding");
        try
        {
            using var keys = new SyntheticAppleKey();
            await using var authDb = CreateDbContext(connection);
            var authentication = CreateAuthentication(authDb, keys);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "apple.onboard@example.invalid", "synthetic-apple-subject-onboard");

            var firstSignIn = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId), default);
            Assert.Equal(SessionOperationStatus.Succeeded, firstSignIn.Status);
            Assert.NotNull(firstSignIn.Tokens);
            Assert.False(firstSignIn.Tokens!.IsOnboardingComplete);
            var userId = firstSignIn.Tokens.UserId;

            await using var onboardingDb = CreateDbContext(connection);
            var onboardingStore = new PostgresCompleteAccountRegistrationStore(onboardingDb, TimeProvider.System);
            var onboardingUseCase = new CompleteAccountOnboardingUseCase(onboardingStore, TimeProvider.System);
            var quiz = await onboardingStore.LoadQuizQuestionsAsync(default);
            DayOfWeek[] availableWeekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
            var selections = quiz
                .Where(question => question.QuestionType != QuizQuestionType.GoalPace)
                .SelectMany(question => question.QuestionType == QuizQuestionType.AvailableWeekdays
                    ? question.Answers.Where(answer => availableWeekdays.Any(day => answer.MappedValue == (int)day))
                        .Select(answer => new QuizSelectionDto(question.Id, answer.Id))
                    : [new QuizSelectionDto(question.Id, question.QuestionType switch
                    {
                        QuizQuestionType.Goal => question.Answers.Single(answer =>
                            answer.MappedValue == (int)UserGoal.MaintainHealth).Id,
                        QuizQuestionType.ExperienceLevel => question.Answers.Single(answer =>
                            answer.MappedValue == (int)TrainingExperienceLevel.Beginner).Id,
                        QuizQuestionType.CurrentCyclePhase => question.Answers.Single(answer =>
                            answer.MappedValue == (int)CyclePhase.Follicular).Id,
                        QuizQuestionType.SessionDuration => question.Answers.First().Id,
                        QuizQuestionType.Equipment => question.Answers.Single(answer =>
                            answer.MappedValue == 1).Id,
                        _ => question.Answers.First().Id
                    })]).ToArray();

            var completion = await onboardingUseCase.ExecuteAsync(
                userId,
                new CompleteAccountOnboardingRequest(
                    new ProfileSetupDraft(
                        "Synthetic Onboarded Member",
                        DateTime.UtcNow.AddYears(-26),
                        166f,
                        60f,
                        string.Empty,
                        28,
                        [(int)WorkoutActivityType.HighVolumeStrength],
                        UseMetricSystem: true),
                    selections),
                default);

            Assert.Equal(CompleteAccountOnboardingStatus.Completed, completion.Status);

            await using var verifyDb = CreateDbContext(connection);
            var completedUser = await verifyDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
            Assert.True(completedUser.IsOnboardingComplete);
            await SyntheticTenantSession.SetAsync(verifyDb, userId);
            var completedProfile = await verifyDb.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
            Assert.Equal("Synthetic Onboarded Member", completedProfile.Name);
            Assert.NotNull(completedProfile.DateOfBirth);
            Assert.Equal(166m, completedProfile.Height);
            Assert.Equal(60m, completedProfile.Weight);
            Assert.Equal("MaintainHealth", completedProfile.Goal);
            Assert.Equal("FullGym", completedProfile.EquipmentLevel);
            Assert.True(await verifyDb.UserQuizResponses.AsNoTracking().AnyAsync(r => r.UserId == userId));
            Assert.True(await verifyDb.CycleLogs.AsNoTracking().AnyAsync(c => c.UserId == userId));

            var (challengeId2, nonce2) = await authentication.CreateAppleNonceAsync(default);
            var token2 = keys.CreateToken(nonce2, "apple.onboard@example.invalid", "synthetic-apple-subject-onboard");
            var secondSignIn = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token2, nonce2, challengeId2), default);
            Assert.Equal(SessionOperationStatus.Succeeded, secondSignIn.Status);
            Assert.True(secondSignIn.Tokens!.IsOnboardingComplete);

            var refreshed = await authentication.RefreshAsync(
                new RefreshSessionRequest(secondSignIn.Tokens.RefreshToken), default);
            Assert.Equal(SessionOperationStatus.Succeeded, refreshed.Status);
            Assert.True(refreshed.Tokens!.IsOnboardingComplete);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task LegacyUnfilledAppleAccountIsRoutedBackToOnboardingOnSignIn()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_legacy_unfilled");
        try
        {
            using var keys = new SyntheticAppleKey();
            var legacyUserId = Guid.CreateVersion7();
            await using (var seed = CreateDbContext(connection))
            {
                seed.Users.Add(new UserRecord
                {
                    Id = legacyUserId,
                    Email = "legacy.unfilled@example.invalid",
                    Username = "legacy_unfilled",
                    AppleUserId = "synthetic-legacy-unfilled-subject",
                    CredentialType = "Apple",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsOnboardingComplete = true
                });
                await seed.SaveChangesAsync();
            }

            await using var db = CreateDbContext(connection);
            var authentication = CreateAuthentication(db, keys);
            var (challengeId, nonce) = await authentication.CreateAppleNonceAsync(default);
            var token = keys.CreateToken(nonce, "legacy.unfilled@example.invalid", "synthetic-legacy-unfilled-subject");

            var result = await authentication.LoginWithAppleAsync(
                new AppleLoginRequest(token, nonce, challengeId), default);

            Assert.Equal(SessionOperationStatus.Succeeded, result.Status);
            Assert.NotNull(result.Tokens);
            Assert.False(result.Tokens!.IsOnboardingComplete);
            await using var verify = CreateDbContext(connection);
            var user = await verify.Users.AsNoTracking().SingleAsync(u => u.Id == legacyUserId);
            Assert.False(user.IsOnboardingComplete);
        }
        finally
        {
            await fixture.DropAsync(connection);
        }
    }

    [PostgresFact]
    public async Task ProfileLoadBackfillsMissingProfileForExistingUserAndReturnsNullForUnknownUser()
    {
        if (!fixture.Available)
            return;

        var connection = await fixture.CreateCopyAsync("apple_profile_backfill");
        try
        {
            var existingUserId = Guid.CreateVersion7();
            await using (var seed = CreateDbContext(connection))
            {
                seed.Users.Add(new UserRecord
                {
                    Id = existingUserId,
                    Email = "legacy.apple@example.invalid",
                    Username = "legacy_apple",
                    AppleUserId = "synthetic-legacy-apple-subject",
                    CredentialType = "Apple",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsOnboardingComplete = true
                });
                await seed.SaveChangesAsync();
            }

            await using var profileDb = CreateDbContext(connection);
            var profileStore = new PostgresProfileDataStore(profileDb, TimeProvider.System);

            var unknownSnapshot = await profileStore.LoadAsync(Guid.CreateVersion7(), default);
            Assert.Null(unknownSnapshot);

            var backfilled = await profileStore.LoadAsync(existingUserId, default);
            Assert.NotNull(backfilled);
            Assert.Equal(1, backfilled!.Version);
            Assert.Null(backfilled.DateOfBirth);

            var saveResult = await profileStore.UpdateWorkoutPreferencesAsync(
                new UpdateServerWorkoutPreferencesRequest(
                    existingUserId,
                    backfilled.Version,
                    [0],
                    [1, 3, 5],
                    new Dictionary<int, int> { [0] = 45 },
                    "HomeDumbbellsBands",
                    new Dictionary<int, int>(),
                    new HashSet<int>(),
                    false),
                default);

            Assert.Equal(ServerProfileWriteStatus.Saved, saveResult.Status);
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
        SyntheticAppleKey keys,
        IAppleAccountTokenRevoker? appleTokenRevoker = null) =>
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
            },
            appleTokenRevoker);

    private sealed class FaultingAppleTokenRevoker(Func<CancellationToken, Task<string?>> exchangeAsync)
        : IAppleAccountTokenRevoker
    {
        public FaultingAppleTokenRevoker(Func<string?> syncAction)
            : this(_ => Task.FromResult(syncAction()))
        {
        }

        public Task<string?> ExchangeAuthorizationCodeForRefreshTokenAsync(
            string? authorizationCode,
            CancellationToken cancellationToken = default) =>
            exchangeAsync(cancellationToken);

        public Task<AppleTokenRevocationResult> RevokeAccountTokenAsync(
            Guid userId,
            string? authorizationCode = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppleTokenRevocationResult(true, true));
    }

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
