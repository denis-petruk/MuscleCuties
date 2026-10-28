using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PhotoAndAccountDeletionPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_";
    private const int CleanupLeaseMinutes = 5;
    private const int ExtendedDeadlineMinutes = 25;
    private const int ShorterDeadlineMinutes = 10;
    private const int ClaimLimit = 1;
    private const int UsernameSuffixLength = 10;
    private const long InitialActiveLeaseGeneration = 3;
    private const long FirstWorkoutPlanRevisionNumber = 1;
    private const string SyntheticWorkoutRulesetVersion = "SyntheticWorkoutRuleset";
    private const string SyntheticTrainingPolicyVersion = "SyntheticTrainingPolicy";

    [Xunit.Fact]
    public async Task PhotoCleanupLeaseCompletesOnceWithFencing()
    {
        var connectionString = RequireLocalTestConnectionString();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        var userId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var cleanupId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);

        database.Users.Add(CreateSyntheticUser(userId, nowUtc));
        database.PhotoObjectCleanups.Add(new PhotoObjectCleanupRecord
        {
            Id = cleanupId,
            UserId = userId,
            PhotoId = photoId,
            S3Key = $"synthetic/photo-cleanup/{photoId:N}",
            Reason = "CancelledUpload",
            NotBeforeUtc = nowUtc,
            CreatedAtUtc = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var store = new PostgresPhotoCleanupStore(database);
            var claimed = await store.ClaimDueAsync(userId, nowUtc, TimeSpan.FromMinutes(CleanupLeaseMinutes), ClaimLimit);
            var item = Xunit.Assert.Single(claimed);
            Xunit.Assert.Equal(cleanupId, item.Id);
            Xunit.Assert.Equal(userId, item.UserId);
            Xunit.Assert.True(await store.CompleteAsync(item, nowUtc));
            Xunit.Assert.False(await store.CompleteAsync(item, nowUtc));

            var persisted = await database.PhotoObjectCleanups.AsNoTracking()
                .SingleAsync(row => row.Id == cleanupId);
            Xunit.Assert.Equal("Completed", persisted.State);
            Xunit.Assert.Null(persisted.LeaseToken);
        }
        finally
        {
            await database.Users.Where(row => row.Id == userId).ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task AccountPurgeRemovesOwnerPhotoMetadataAndPreservesOtherOwner()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var ownerId = Guid.CreateVersion7();
        var otherOwnerId = Guid.CreateVersion7();
        var ownerPhotoId = Guid.CreateVersion7();
        var otherPhotoId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        var ownerUploadKey = PhotoObjectKeys.Upload(ownerId, ownerPhotoId);
        var ownerPublishedKey = PhotoObjectKeys.Published(ownerId, ownerPhotoId);
        var otherOwnerPublishedKey = PhotoObjectKeys.Published(otherOwnerId, otherPhotoId);

        await SyntheticTenantSession.SetAsync(database, ownerId);
        database.Users.Add(CreateSyntheticUser(ownerId, nowUtc));
        AddSyntheticWorkoutProposalAndRevision(database, ownerId, nowUtc);
        var ownerPlanId = Guid.NewGuid();
        var ownerDayId = Guid.NewGuid();
        database.WorkoutPlans.Add(new WorkoutPlanRecord { Id = ownerPlanId, UserId = ownerId,
            Name = "Synthetic", IsActive = true, CreatedAt = nowUtc });
        database.WorkoutDays.Add(new WorkoutDayRecord { Id = ownerDayId, UserId = ownerId, WorkoutPlanId = ownerPlanId,
            Name = "Synthetic", WorkoutType = "Strength", Date = DateOnly.FromDateTime(nowUtc) });
        database.WorkoutCycleReadinessOccurrences.Add(new WorkoutCycleReadinessOccurrenceRecord
        {
            Id = Guid.NewGuid(), UserId = ownerId, WorkoutDayId = ownerDayId,
            LocalDate = DateOnly.FromDateTime(nowUtc), InputFingerprint = "synthetic",
            PrescriptionJson = "{}", UpdatedAtUtc = nowUtc
        });
        database.PhotoJourneyEntries.Add(new PhotoJourneyEntryRecord
        {
            Id = ownerPhotoId,
            UserId = ownerId,
            Date = DateOnly.FromDateTime(nowUtc),
            ImagePath = ownerPublishedKey,
            CreatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        await SyntheticTenantSession.SetAsync(database, otherOwnerId);
        database.Users.Add(CreateSyntheticUser(otherOwnerId, nowUtc));
        AddSyntheticWorkoutProposalAndRevision(database, otherOwnerId, nowUtc);
        database.PhotoJourneyEntries.Add(new PhotoJourneyEntryRecord
        {
            Id = otherPhotoId,
            UserId = otherOwnerId,
            Date = DateOnly.FromDateTime(nowUtc),
            ImagePath = otherOwnerPublishedKey,
            CreatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            await SyntheticTenantSession.SetAsync(database, ownerId);
            var result = await new PostgresAccountPurgeStore(database).PurgeAccountDataAsync(ownerId);
            Xunit.Assert.True(result.Succeeded);
            Xunit.Assert.Contains(ownerUploadKey, result.CleanedMediaKeys);
            Xunit.Assert.Contains(ownerPublishedKey, result.CleanedMediaKeys);
            Xunit.Assert.False(await database.Users.AsNoTracking().AnyAsync(row => row.Id == ownerId));
            Xunit.Assert.False(await database.PhotoJourneyEntries.AsNoTracking()
                .AnyAsync(row => row.UserId == ownerId));
            Xunit.Assert.False(await database.WorkoutPlanProposalsV02.AsNoTracking()
                .AnyAsync(row => row.UserId == ownerId));
            Xunit.Assert.False(await database.WorkoutPlanRevisionsV02.AsNoTracking()
                .AnyAsync(row => row.UserId == ownerId));
            Xunit.Assert.False(await database.WorkoutCycleReadinessOccurrences.AsNoTracking()
                .AnyAsync(row => row.UserId == ownerId));

            // Durable reconciliation targets survive account deletion.
            var persistedTargets = await database.PhotoObjectReconcileTargets.AsNoTracking()
                .Where(row => row.TargetKey == ownerUploadKey || row.TargetKey == ownerPublishedKey)
                .ToListAsync();
            Xunit.Assert.Equal(2, persistedTargets.Count);
            Xunit.Assert.All(persistedTargets, target => Xunit.Assert.True(target.NotBeforeUtc > nowUtc));

            await SyntheticTenantSession.SetAsync(database, otherOwnerId);
            Xunit.Assert.True(await database.Users.AsNoTracking().AnyAsync(row => row.Id == otherOwnerId));
            Xunit.Assert.True(await database.PhotoJourneyEntries.AsNoTracking()
                .AnyAsync(row => row.UserId == otherOwnerId));
            Xunit.Assert.True(await database.WorkoutPlanProposalsV02.AsNoTracking()
                .AnyAsync(row => row.UserId == otherOwnerId));
            Xunit.Assert.True(await database.WorkoutPlanRevisionsV02.AsNoTracking()
                .AnyAsync(row => row.UserId == otherOwnerId));
            Xunit.Assert.False(await database.PhotoObjectReconcileTargets.AsNoTracking()
                .AnyAsync(row => row.TargetKey == otherOwnerPublishedKey));
        }
        finally
        {
            await database.PhotoObjectReconcileTargets
                .Where(row => row.TargetKey == ownerUploadKey || row.TargetKey == ownerPublishedKey)
                .ExecuteDeleteAsync();
            await SyntheticTenantSession.SetAsync(database, otherOwnerId);
            await database.WorkoutPlanRevisionsV02.Where(row => row.UserId == otherOwnerId).ExecuteDeleteAsync();
            await database.WorkoutPlanProposalsV02.Where(row => row.UserId == otherOwnerId).ExecuteDeleteAsync();
            await database.Users.Where(row => row.Id == otherOwnerId).ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgePersistsCanonicalUploadAndPublishedTargets()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var ownerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var uploadKey = PhotoObjectKeys.Upload(ownerId, photoId);
        var publishedKey = PhotoObjectKeys.Published(ownerId, photoId);
        var dueUtc = DateTime.UtcNow.AddMinutes(CleanupLeaseMinutes);

        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync();
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, ownerId);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT app.stage_account_photo_purge({ownerId}, {uploadKey}, {dueUtc})");
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT app.stage_account_photo_purge({ownerId}, {publishedKey}, {dueUtc})");
            await transaction.CommitAsync();

            var targets = await database.PhotoObjectReconcileTargets.AsNoTracking()
                .Where(row => row.TargetKey == uploadKey || row.TargetKey == publishedKey)
                .ToListAsync();
            Xunit.Assert.Equal(2, targets.Count);
        }
        finally
        {
            await database.PhotoObjectReconcileTargets
                .Where(row => row.TargetKey == uploadKey || row.TargetKey == publishedKey)
                .ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgeRejectsMissingOrEmptyTenantContext()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var ownerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var uploadKey = PhotoObjectKeys.Upload(ownerId, photoId);
        var dueUtc = DateTime.UtcNow.AddMinutes(CleanupLeaseMinutes);

        // 1. Absent (unset) tenant context
        await using (var tx1 = await database.Database.BeginTransactionAsync())
        {
            await database.Database.ExecuteSqlRawAsync("RESET app.user_id");
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({ownerId}, {uploadKey}, {dueUtc})"));
            await tx1.RollbackAsync();
        }

        // 2. Empty tenant context
        await using (var tx2 = await database.Database.BeginTransactionAsync())
        {
            await database.Database.ExecuteSqlRawAsync("SELECT set_config('app.user_id', '', true)");
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({ownerId}, {uploadKey}, {dueUtc})"));
            await tx2.RollbackAsync();
        }

        // 3. Empty GUID tenant context
        await using (var tx3 = await database.Database.BeginTransactionAsync())
        {
            await database.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.user_id', '00000000-0000-0000-0000-000000000000', true)");
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({Guid.Empty}, {uploadKey}, {dueUtc})"));
            await tx3.RollbackAsync();
        }

        Xunit.Assert.False(await database.PhotoObjectReconcileTargets.AsNoTracking()
            .AnyAsync(row => row.TargetKey == uploadKey));
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgeRejectsMismatchedOwnerAndNonCanonicalKeys()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var activeOwnerId = Guid.CreateVersion7();
        var otherOwnerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var activeOwnerKey = PhotoObjectKeys.Upload(activeOwnerId, photoId);
        var otherOwnerKey = PhotoObjectKeys.Upload(otherOwnerId, photoId);
        var malformedKey = $"photos/{activeOwnerId:D}/../{photoId:N}";
        var dueUtc = DateTime.UtcNow.AddMinutes(CleanupLeaseMinutes);

        await using (var tx1 = await database.Database.BeginTransactionAsync())
        {
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, activeOwnerId);
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({otherOwnerId}, {otherOwnerKey}, {dueUtc})"));
            await tx1.RollbackAsync();
        }

        await using (var tx2 = await database.Database.BeginTransactionAsync())
        {
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, activeOwnerId);
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({activeOwnerId}, {otherOwnerKey}, {dueUtc})"));
            await tx2.RollbackAsync();
        }

        await using (var tx3 = await database.Database.BeginTransactionAsync())
        {
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, activeOwnerId);
            await Xunit.Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({activeOwnerId}, {malformedKey}, {dueUtc})"));
            await tx3.RollbackAsync();
        }

        Xunit.Assert.False(await database.PhotoObjectReconcileTargets.AsNoTracking()
            .AnyAsync(row => row.TargetKey == activeOwnerKey || row.TargetKey == otherOwnerKey));
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgeDuplicatePreservesLongerDeadlineAndActiveLease()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var ownerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var targetKey = PhotoObjectKeys.Published(ownerId, photoId);
        var nowUtc = DateTime.UtcNow;
        var initialDeadlineUtc = new DateTime(nowUtc.AddMinutes(ShorterDeadlineMinutes).Ticks / 10 * 10, DateTimeKind.Utc);
        var laterDeadlineUtc = new DateTime(nowUtc.AddMinutes(ExtendedDeadlineMinutes).Ticks / 10 * 10, DateTimeKind.Utc);
        var earlierDeadlineUtc = new DateTime(nowUtc.AddMinutes(CleanupLeaseMinutes).Ticks / 10 * 10, DateTimeKind.Utc);
        var activeLeaseToken = Guid.CreateVersion7();
        var activeLeaseExpiresUtc = new DateTime(nowUtc.AddMinutes(CleanupLeaseMinutes).Ticks / 10 * 10, DateTimeKind.Utc);

        try
        {
            await using (var tx1 = await database.Database.BeginTransactionAsync())
            {
                await PostgresTenantContext.SetForCurrentTransactionAsync(database, ownerId);
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({ownerId}, {targetKey}, {initialDeadlineUtc})");
                await tx1.CommitAsync();
            }

            await database.PhotoObjectReconcileTargets
                .Where(row => row.TargetKey == targetKey)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(row => row.LeaseToken, activeLeaseToken)
                    .SetProperty(row => row.LeaseGeneration, InitialActiveLeaseGeneration)
                    .SetProperty(row => row.LeaseExpiresAtUtc, activeLeaseExpiresUtc));

            // Duplicate staging with a later deadline extends not_before_utc without clearing the active lease.
            await using (var tx2 = await database.Database.BeginTransactionAsync())
            {
                await PostgresTenantContext.SetForCurrentTransactionAsync(database, ownerId);
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({ownerId}, {targetKey}, {laterDeadlineUtc})");
                await tx2.CommitAsync();
            }

            // Duplicate staging with an earlier deadline never shortens not_before_utc or invalidates the lease.
            await using (var tx3 = await database.Database.BeginTransactionAsync())
            {
                await PostgresTenantContext.SetForCurrentTransactionAsync(database, ownerId);
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT app.stage_account_photo_purge({ownerId}, {targetKey}, {earlierDeadlineUtc})");
                await tx3.CommitAsync();
            }

            var persisted = await database.PhotoObjectReconcileTargets.AsNoTracking()
                .SingleAsync(row => row.TargetKey == targetKey);
            Xunit.Assert.Equal(laterDeadlineUtc, persisted.NotBeforeUtc);
            Xunit.Assert.Equal(activeLeaseToken, persisted.LeaseToken);
            Xunit.Assert.Equal(InitialActiveLeaseGeneration, persisted.LeaseGeneration);
            Xunit.Assert.Equal(activeLeaseExpiresUtc, persisted.LeaseExpiresAtUtc);
        }
        finally
        {
            await database.PhotoObjectReconcileTargets
                .Where(row => row.TargetKey == targetKey)
                .ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgeRollsBackWhenTransactionAborts()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        await EnsureMigrationsAppliedAsync(database);

        var ownerId = Guid.CreateVersion7();
        var photoId = Guid.CreateVersion7();
        var targetKey = PhotoObjectKeys.Upload(ownerId, photoId);
        var dueUtc = DateTime.UtcNow.AddMinutes(CleanupLeaseMinutes);

        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, ownerId);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT app.stage_account_photo_purge({ownerId}, {targetKey}, {dueUtc})");
            await transaction.RollbackAsync();
        }

        Xunit.Assert.False(await database.PhotoObjectReconcileTargets.AsNoTracking()
            .AnyAsync(row => row.TargetKey == targetKey));
    }

    [Xunit.Fact]
    public async Task StageAccountPhotoPurgeEnforcesRestrictedSecurityDefinerAndBlocksDirectAppTableAccess()
    {
        var connectionString = RequireLocalTestConnectionString();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString).Options;

        await using (var database = new PostgresDbContext(options))
            await EnsureMigrationsAppliedAsync(database);


        await using var cmd = new NpgsqlCommand("""
            SELECT p.prosecdef,
                   r.rolname,
                   r.rolcanlogin,
                   r.rolsuper,
                   r.rolbypassrls,
                   r.rolcreatedb,
                   r.rolcreaterole,
                   has_function_privilege('public', p.oid, 'EXECUTE') AS public_exec,
                   has_function_privilege('musclecuties_app', p.oid, 'EXECUTE') AS app_exec,
                   has_table_privilege('musclecuties_app', 'app.photo_object_reconcile_targets', 'SELECT, INSERT, UPDATE, DELETE') AS app_table_priv,
                   pg_has_role('musclecuties_app', 'musclecuties_photo_purge_stage_owner', 'MEMBER') AS app_inherits_stage_owner,
                   pg_has_role('musclecuties_app', 'musclecuties_photo_reconcile', 'MEMBER') AS app_inherits_reconcile
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            JOIN pg_roles r ON r.oid = p.proowner
            WHERE n.nspname = 'app' AND p.proname = 'stage_account_photo_purge';
            """, connection);

        await using var reader = await cmd.ExecuteReaderAsync();
        Xunit.Assert.True(await reader.ReadAsync());
        Xunit.Assert.True(reader.GetBoolean(reader.GetOrdinal("prosecdef")));
        Xunit.Assert.Equal("musclecuties_photo_purge_stage_owner", reader.GetString(reader.GetOrdinal("rolname")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("rolcanlogin")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("rolsuper")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("rolbypassrls")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("rolcreatedb")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("rolcreaterole")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("public_exec")));
        Xunit.Assert.True(reader.GetBoolean(reader.GetOrdinal("app_exec")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("app_table_priv")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("app_inherits_stage_owner")));
        Xunit.Assert.False(reader.GetBoolean(reader.GetOrdinal("app_inherits_reconcile")));
    }

    private static async Task EnsureMigrationsAppliedAsync(PostgresDbContext database)
    {
        if ((await database.Database.GetPendingMigrationsAsync()).Any())
            await database.Database.MigrateAsync();
    }

    private static void AddSyntheticWorkoutProposalAndRevision(
        PostgresDbContext database, Guid userId, DateTime nowUtc)
    {
        var proposalId = Guid.CreateVersion7();
        var revisionId = Guid.CreateVersion7();
        var planId = Guid.CreateVersion7();
        database.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record
        {
            Id = proposalId,
            UserId = userId,
            PlanId = planId,
            ExpectedRevisionId = Guid.CreateVersion7(),
            RulesetVersion = SyntheticWorkoutRulesetVersion,
            TrainingPolicyVersion = SyntheticTrainingPolicyVersion,
            Status = "Accepted",
            CreatedAtUtc = nowUtc,
            DecidedAtUtc = nowUtc,
            AcceptedRevisionId = revisionId
        });
        database.WorkoutPlanRevisionsV02.Add(new WorkoutPlanRevisionV02Record
        {
            Id = revisionId,
            UserId = userId,
            PlanId = planId,
            RevisionNumber = FirstWorkoutPlanRevisionNumber,
            SourceProposalId = proposalId,
            RulesetVersion = SyntheticWorkoutRulesetVersion,
            TrainingPolicyVersion = SyntheticTrainingPolicyVersion,
            AcceptedAtUtc = nowUtc
        });
    }

    private static UserRecord CreateSyntheticUser(Guid userId, DateTime nowUtc) => new()
    {
        Id = userId,
        Email = $"photo-db-{userId:N}@example.invalid",
        Username = $"photo_{userId.ToString("N")[^UsernameSuffixLength..]}",
        CredentialType = "Password",
        PasswordHash = "synthetic-hash",
        CreatedAt = nowUtc,
        UpdatedAt = nowUtc,
        IsOnboardingComplete = true
    };

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
