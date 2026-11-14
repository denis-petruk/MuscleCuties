using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Server;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Workout.Injury;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjuryUiHttpPostgresRoundTripTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_test_";
    private const string SigningKey = "synthetic-injury-roundtrip-key-for-tests-only";
    private const string Issuer = "SyntheticInjuryServer";
    private const string Audience = "SyntheticInjuryClient";
    private const int InitialPain = 3;
    private const int EditedPain = 5;
    private const int TokenLifetimeMinutes = 5;

    [PostgresFact]
    public async Task InjuryAnswersSurviveViewModelSaveReadEditAndClearThroughHttpAndPostgres()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(configured), $"Set {ConnectionVariable} to a disposable local database.");
        var connection = new NpgsqlConnectionStringBuilder(configured);
        Assert.Equal(LocalHost, connection.Host);
        Assert.StartsWith(SyntheticDatabasePrefix, connection.Database, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(configured).Options;
        await using var db = new PostgresDbContext(options);
        var owner = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(db, owner);
        db.Users.Add(new UserRecord
        {
            Id = owner, Email = $"injury-{owner:N}@example.invalid",
            Username = $"inj_{owner.ToString("N")[..10]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true
        });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = owner, TimeZoneId = "UTC"
        });
        await db.SaveChangesAsync();
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = configured,
                ["Authentication:SigningKey"] = SigningKey,
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:Audience"] = Audience
            });
            await using var app = ApiApplication.Build(builder, services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IAccountAuthenticationUseCase>();
                services.AddSingleton<IAccountAuthenticationUseCase>(new SyntheticAuthentication());
            });
            await app.StartAsync();
            using var http = app.GetTestClient();
            using var anonymous = await http.GetAsync("/api/v1/injuries");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            var token = CreateAccessToken(owner);
            var api = new MuscleCutiesBackendApiClient(http, new TokenStore(owner, token), new UnusedCache(), new OnlineGate());
            var cloud = new CloudWorkoutInjuryStore(api);
            var useCase = new WorkoutInjuryUseCase(cloud);
            var viewModel = new InjuryLogViewModel(useCase, () => Task.CompletedTask);
            await viewModel.LoadCommand.ExecuteAsync(null);
            Assert.False(viewModel.IsLoadError);
            viewModel.ToggleAddFormCommand.Execute(null);
            viewModel.SelectedSiteFlag = WorkoutInjurySite.Shoulder;
            viewModel.SelectedStatus = WorkoutInjuryStatus.Acute;
            viewModel.NewPainLevel = InitialPain;
            viewModel.SelectedWorseningOption = viewModel.WorseningTriggerOptions.Single(
                option => option.Trigger == InjuryWorseningTrigger.OverheadReach);
            viewModel.ClinicianAdvisedAvoid = true;
            await viewModel.SaveInjuryCommand.ExecuteAsync(null);

            var freshViewModel = new InjuryLogViewModel(useCase, () => Task.CompletedTask);
            await freshViewModel.LoadCommand.ExecuteAsync(null);
            Assert.False(freshViewModel.IsLoadError);
            var loaded = Assert.Single(freshViewModel.ActiveInjuries);
            Assert.Equal(InjuryWorseningTrigger.OverheadReach, loaded.WorseningTrigger);
            Assert.True(loaded.ClinicianAdvisedAvoid);
            freshViewModel.EditInjuryCommand.Execute(loaded);
            freshViewModel.NewPainLevel = EditedPain;
            await freshViewModel.SaveInjuryCommand.ExecuteAsync(null);

            db.ChangeTracker.Clear();
            var edited = await db.WorkoutInjuryLogs.AsNoTracking().SingleAsync(row => row.UserId == owner);
            Assert.Equal(EditedPain, edited.Pain);
            Assert.Equal(nameof(InjuryWorseningTrigger.OverheadReach), edited.WorseningTrigger);
            Assert.True(edited.ClinicianAdvisedAvoid);

            // The same answers must reach planning, not only the injury screen. Overhead reach
            // blocks vertical pressing/pulling; the clinician restriction expands through the
            // reviewed shoulder contraindications even though the trigger is answered.
            await using (var planningTransaction = await db.Database.BeginTransactionAsync())
            {
                await SyntheticTenantSession.SetAsync(db, owner);
                var planningSource = await new PostgresStrengthPlanPreviewSourceV020(db)
                    .LoadInCurrentTransactionAsync(owner, CancellationToken.None);
                Assert.NotNull(planningSource);
                Assert.Contains(MovementPattern.VerticalPush, planningSource!.BlockedMovementPatterns!);
                var shoulderPatterns = planningSource.Catalog
                    .Where(entry => entry.Candidate.Contraindications is { } flags &&
                        (flags & InjuryFlag.Shoulder) != InjuryFlag.None)
                    .Select(entry => entry.Candidate.Pattern)
                    .ToHashSet();
                Assert.NotEmpty(shoulderPatterns);
                Assert.All(shoulderPatterns,
                    pattern => Assert.Contains(pattern, planningSource.BlockedMovementPatterns!));
                await planningTransaction.RollbackAsync();
            }

            var reload = new InjuryLogViewModel(useCase, () => Task.CompletedTask);
            await reload.LoadCommand.ExecuteAsync(null);
            await reload.MarkClearedCommand.ExecuteAsync(Assert.Single(reload.ActiveInjuries));
            db.ChangeTracker.Clear();
            var cleared = await db.WorkoutInjuryLogs.AsNoTracking().SingleAsync(row => row.UserId == owner);
            Assert.Equal(InjuryObservationRule.ClearedStatus, cleared.Status);
            Assert.Equal(nameof(InjuryWorseningTrigger.OverheadReach), cleared.WorseningTrigger);
            Assert.True(cleared.ClinicianAdvisedAvoid);
        }
        finally
        {
            await db.Users.Where(user => user.Id == owner).ExecuteDeleteAsync();
        }
    }

    private static string CreateAccessToken(Guid owner) =>
        new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("sub", owner.ToString("D")),
                new Claim("jti", Guid.NewGuid().ToString("D")),
                new Claim("sid", Guid.NewGuid().ToString("D"))]),
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)
        });

    private sealed class SyntheticAuthentication : IAccountAuthenticationUseCase
    {
        public Task<bool> IsAccessTokenRevokedAsync(string tokenId, string sessionId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<(string ChallengeId, string Nonce)> CreateAppleNonceAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionOperationResult> LoginWithPasswordAsync(PasswordLoginRequest request, string? trustedSourceAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionOperationResult> LoginWithAppleAsync(AppleLoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SessionOperationResult> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RevokeAsync(RevokeSessionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TokenStore(Guid owner, string token) : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendSessionTokens?>(new(owner, token, string.Empty, DateTimeOffset.UtcNow.AddMinutes(TokenLifetimeMinutes)));
        public Task WriteTokensAsync(BackendSessionTokens tokens, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ClearTokensAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OnlineGate : ICloudAuthorityGate
    {
        public int AuthorityVersion => 1;
        public CloudAuthorityMode CurrentMode => CloudAuthorityMode.ServerAuthoritativeCutover;
        public bool IsNetworkOnline => true;
        public void SetAuthorityMode(CloudAuthorityMode mode) => throw new NotSupportedException();
        public void SetNetworkOnline(bool isOnline) => throw new NotSupportedException();
        public OfflineWriteCheckResult EvaluateWriteAllowed(string operationName) => new(true, null, null);
    }

    private sealed class UnusedCache : ILocalCacheService
    {
        public Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveActiveWorkoutSnapshotAsync(Guid accountId, ActiveWorkoutSessionSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveCycleStateSnapshotAsync(Guid accountId, CycleStateSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveNutritionTargetsSnapshotAsync(Guid accountId, DailyNutritionTargetsSnapshot snapshot, DateTimeOffset capturedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ClearAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
