using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>G-10: equivalent offer-creation paths must leave one active offer per plan.</summary>
public sealed class StrengthStyleOfferStoreIntegrationTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DatabasePrefix = "mc_test_";
    private static readonly int CurrentStyleId = (int)WorkoutActivityType.GeneralStrength;
    private static readonly int AlternativeStyleId = (int)WorkoutActivityType.StrengthHighIntensity;

    [PostgresFact]
    public async Task DuplicateAndSubsequentCreationLeavesOneActiveOfferPerPlan()
    {
        await using var database = Open();
        var userId = Guid.NewGuid();
        await using var transaction = await database.Database.BeginTransactionAsync();
        await SyntheticTenantSession.SetAsync(database, userId);
        var now = DateTime.UtcNow;
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"offer-{userId:N}@example.invalid", Username = $"off_{userId.ToString("N")[..10]}",
            CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now
        });
        await database.SaveChangesAsync();
        try
        {
            var planId = Guid.NewGuid();
            var firstRevision = Guid.NewGuid();
            var store = new PostgresStrengthStyleOfferStore(database);

            await store.CreateInCurrentTransactionAsync(userId, planId, firstRevision, CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);
            // Same revision again: the store is idempotent, not merely protected by the index.
            await store.CreateInCurrentTransactionAsync(userId, planId, firstRevision, CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);
            // A later revision of the same plan must not stack a second active prompt.
            await store.CreateInCurrentTransactionAsync(userId, planId, Guid.NewGuid(), CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);

            var active = await database.StrengthStyleOffers.AsNoTracking()
                .Where(row => row.UserId == userId && row.DismissedAtUtc == null).ToListAsync();
            Assert.Single(active);
            Assert.Equal(firstRevision, active[0].RevisionId);

            // Dismissing the offer frees the plan for a genuinely new prompt.
            var stored = await database.StrengthStyleOffers.SingleAsync(row => row.Id == active[0].Id);
            stored.DismissedAtUtc = now;
            await database.SaveChangesAsync();
            await store.CreateInCurrentTransactionAsync(userId, planId, Guid.NewGuid(), CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);
            Assert.Equal(2, await database.StrengthStyleOffers.AsNoTracking()
                .CountAsync(row => row.UserId == userId));

            // A different plan keeps its own single active offer.
            var otherPlanId = Guid.NewGuid();
            await store.CreateInCurrentTransactionAsync(userId, otherPlanId, Guid.NewGuid(), CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);
            await store.CreateInCurrentTransactionAsync(userId, otherPlanId, Guid.NewGuid(), CurrentStyleId,
                [AlternativeStyleId], CancellationToken.None);
            Assert.Single(await database.StrengthStyleOffers.AsNoTracking()
                .Where(row => row.UserId == userId && row.PlanId == otherPlanId && row.DismissedAtUtc == null)
                .ToListAsync());
        }
        finally
        {
            await transaction.RollbackAsync();
            database.ChangeTracker.Clear();
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static PostgresDbContext Open()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(LocalHost, builder.Host);
        Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options);
    }
}
