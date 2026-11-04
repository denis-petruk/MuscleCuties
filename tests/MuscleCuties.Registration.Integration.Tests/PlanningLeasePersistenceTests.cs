using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PlanningLeasePersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_test_";
    private const string SyntheticRuleset = "synthetic-ruleset";
    private const string SyntheticPolicy = "synthetic-policy";
    private const long InitialFence = 1;
    private const int InitialProposalVersion = 1;
    private const long FirstTakeoverFence = InitialFence + 1;
    private const long SecondTakeoverFence = FirstTakeoverFence + 1;

    [Xunit.Fact]
    public async Task ConcurrentTakeoversFencePreviousDevicesAndRejectTheirApply()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var setup = new PostgresDbContext(options);
        var userId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var initialLease = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(setup, userId);
        setup.Users.Add(new UserRecord
        {
            Id = userId, Email = $"lease-{userId:N}@example.invalid",
            Username = $"lease_{userId.ToString("N")[..10]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true
        });
        setup.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record
        {
            Id = proposalId, UserId = userId, PlanId = Guid.NewGuid(), ExpectedRevisionId = Guid.NewGuid(),
            RulesetVersion = SyntheticRuleset, TrainingPolicyVersion = SyntheticPolicy,
            Status = "Proposed", CreatedAtUtc = now, LeaseId = initialLease, LeaseFence = InitialFence,
            LeaseExpiresAtUtc = now.Add(PlanningLifecycleRule.FiveMinuteLeaseDuration),
            ConcurrencyVersion = InitialProposalVersion
        });
        await setup.SaveChangesAsync();
        try
        {
            await using var deviceA = new PostgresDbContext(options);
            await using var deviceB = new PostgresDbContext(options);
            var storeA = new PostgresStrengthPlanProposalStoreV020(deviceA);
            var storeB = new PostgresStrengthPlanProposalStoreV020(deviceB);
            var outcomes = await Task.WhenAll(
                storeA.TakeoverAsync(userId, proposalId, CancellationToken.None),
                storeB.TakeoverAsync(userId, proposalId, CancellationToken.None));

            Xunit.Assert.All(outcomes, result => Xunit.Assert.Equal("Active", result.Status));
            var leases = outcomes.Select(result => Xunit.Assert.IsType<PlanningLeaseDto>(result.Lease))
                .OrderBy(lease => lease.Fence).ToArray();
            Xunit.Assert.Equal(FirstTakeoverFence, leases[0].Fence);
            Xunit.Assert.Equal(SecondTakeoverFence, leases[1].Fence);
            Xunit.Assert.NotEqual(leases[0].LeaseId, leases[1].LeaseId);
            var active = await storeA.GetActivePreviewAsync(userId, CancellationToken.None);
            Xunit.Assert.Equal(proposalId, active.ProposalId);

            var staleRenewal = await storeA.RenewAsync(userId,
                new PlanningLeaseRequest(proposalId, leases[0].LeaseId, leases[0].Fence), CancellationToken.None);
            Xunit.Assert.Equal("Stale", staleRenewal.Status);
            var staleApply = await new PostgresAcceptStrengthPlanProposalStoreV020(deviceB).AcceptAsync(userId,
                new AcceptStrengthPlanProposalRequestV020(proposalId, initialLease, InitialFence, InitialProposalVersion),
                CancellationToken.None);
            Xunit.Assert.Equal("Stale", staleApply.Status);
            Xunit.Assert.Equal("PreviewLeaseExpiredOrInvalid", staleApply.LimitationCode);
            var wrongVersion = await new PostgresAcceptStrengthPlanProposalStoreV020(deviceA).AcceptAsync(userId,
                new AcceptStrengthPlanProposalRequestV020(proposalId, leases[1].LeaseId, leases[1].Fence,
                    InitialProposalVersion + 1), CancellationToken.None);
            Xunit.Assert.Equal("Stale", wrongVersion.Status);
            var invalidSnapshot = await new PostgresAcceptStrengthPlanProposalStoreV020(deviceA).AcceptAsync(userId,
                new AcceptStrengthPlanProposalRequestV020(proposalId, leases[1].LeaseId, leases[1].Fence,
                    InitialProposalVersion), CancellationToken.None);
            Xunit.Assert.Equal("InvalidProposal", invalidSnapshot.Status);
            Xunit.Assert.False(await setup.WorkoutPlans.AsNoTracking().AnyAsync(item => item.UserId == userId));
            Xunit.Assert.False(await setup.WorkoutPlanRevisionsV02.AsNoTracking().AnyAsync(item => item.UserId == userId));
            var persisted = await setup.WorkoutPlanProposalsV02.AsNoTracking().SingleAsync(item => item.Id == proposalId);
            Xunit.Assert.Equal(leases[1].LeaseId, persisted.LeaseId);
            Xunit.Assert.Equal(leases[1].Fence, persisted.LeaseFence);
            Xunit.Assert.Equal("Proposed", persisted.Status);
        }
        finally
        {
            await setup.WorkoutPlanProposalsV02.Where(item => item.UserId == userId).ExecuteDeleteAsync();
            await setup.Users.Where(item => item.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return connectionString!;
    }
}
