using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjuryEpisodeIsolationPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_test_";
    private const int SyntheticSite = 1;
    private const int SyntheticSets = 1;
    private const int SyntheticRepetitions = 1;
    private const string UtcZone = "UTC";

    [Xunit.Fact]
    public async Task DeletedInjurySourceRetainsEpisodeAndPendingSafetyReconciliation()
    {
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var owner = Guid.NewGuid();
        var injury = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(db, owner);
        AddUser(db, owner, now);
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = owner, TimeZoneId = UtcZone, SafetyRegenerationPending = true
        });
        db.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = Guid.NewGuid(), UserId = owner, Name = "Synthetic plan", IsActive = true,
            CyclePhaseTarget = "Follicular", CreatedAt = now
        });
        db.WorkoutInjuryLogs.Add(new WorkoutInjuryLogRecord
        {
            Id = injury, UserId = owner, SiteFlag = SyntheticSite, Status = "Acute",
            Since = DateOnly.FromDateTime(now), Date = DateOnly.FromDateTime(now), CreatedAt = now
        });
        db.WorkoutInjuryEpisodes.Add(new WorkoutInjuryEpisodeRecord
        {
            Id = Guid.NewGuid(), UserId = owner, InjuryLogId = injury, SiteFlag = SyntheticSite,
            StartedOn = DateOnly.FromDateTime(now), SourceDeleted = true
        });
        await db.SaveChangesAsync();
        await db.WorkoutInjuryLogs.Where(row => row.Id == injury).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        await new PostgresWorkoutPlanReconciler(db).ReconcileInCurrentTransactionAsync(owner);
        Xunit.Assert.False(await db.WorkoutInjuryLogs.AnyAsync(row => row.UserId == owner));
        Xunit.Assert.True(await db.WorkoutInjuryEpisodes.AnyAsync(row => row.UserId == owner && row.SourceDeleted));
        Xunit.Assert.True(await db.WorkoutAccountPlanningStates.Where(row => row.UserId == owner)
            .Select(row => row.SafetyRegenerationPending).SingleAsync());
        Xunit.Assert.False(await db.WorkoutPlanRevisionsV02.AnyAsync(row => row.UserId == owner));
        await transaction.RollbackAsync();
    }

    [Xunit.Fact]
    public async Task EpisodeAndPrescriptionLinksRespectTenantIsolation()
    {
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var owners = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var exerciseId = await db.Exercises.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
        var episodes = new List<Guid>();
        var prescriptions = new List<Guid>();
        foreach (var owner in owners)
        {
            await SyntheticTenantSession.SetAsync(db, owner);
            AddUser(db, owner, now);
            var plan = Guid.NewGuid();
            var day = Guid.NewGuid();
            var prescription = Guid.NewGuid();
            var episode = Guid.NewGuid();
            prescriptions.Add(prescription);
            episodes.Add(episode);
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = plan, UserId = owner, Name = "Synthetic plan",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = day, UserId = owner, WorkoutPlanId = plan,
                DayOfWeek = (int)now.DayOfWeek, Date = DateOnly.FromDateTime(now), WorkoutType = "Strength", Name = "Synthetic day" });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = prescription, UserId = owner,
                WorkoutDayId = day, ExerciseId = exerciseId, Sets = SyntheticSets, Reps = SyntheticRepetitions });
            db.WorkoutInjuryEpisodes.Add(new WorkoutInjuryEpisodeRecord { Id = episode, UserId = owner,
                InjuryLogId = Guid.NewGuid(), SiteFlag = SyntheticSite, StartedOn = DateOnly.FromDateTime(now) });
            db.WorkoutInjuryPrescriptionLinks.Add(new WorkoutInjuryPrescriptionLinkRecord { Id = Guid.NewGuid(),
                UserId = owner, WorkoutDayExerciseId = prescription, InjuryEpisodeId = episode, OriginalExerciseId = exerciseId });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        await db.Database.ExecuteSqlRawAsync("SET LOCAL ROLE musclecuties_app");
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var permissions = new NpgsqlCommand("""
            SELECT has_table_privilege(current_user, 'app.workout_injury_episodes', 'SELECT') AND
                   has_table_privilege(current_user, 'app.workout_injury_episodes', 'INSERT') AND
                   has_table_privilege(current_user, 'app.workout_injury_episodes', 'UPDATE') AND
                   has_table_privilege(current_user, 'app.workout_injury_episodes', 'DELETE'),
                   has_table_privilege(current_user, 'app.workout_injury_prescription_links', 'SELECT') AND
                   has_table_privilege(current_user, 'app.workout_injury_prescription_links', 'INSERT') AND
                   has_table_privilege(current_user, 'app.workout_injury_prescription_links', 'DELETE'),
                   has_table_privilege(current_user, 'app.workout_injury_prescription_links', 'UPDATE'),
                   has_table_privilege(current_user, 'app.workout_injury_episodes', 'TRUNCATE'),
                   has_table_privilege(current_user, 'app.workout_injury_prescription_links', 'TRUNCATE')
            """, connection))
        await using (var permissionReader = await permissions.ExecuteReaderAsync())
        {
            Xunit.Assert.True(await permissionReader.ReadAsync());
            Xunit.Assert.True(permissionReader.GetBoolean(0));
            Xunit.Assert.True(permissionReader.GetBoolean(1));
            Xunit.Assert.False(permissionReader.GetBoolean(2));
            Xunit.Assert.False(permissionReader.GetBoolean(3));
            Xunit.Assert.False(permissionReader.GetBoolean(4));
        }
        await SyntheticTenantSession.SetAsync(db, owners[0]);
        Xunit.Assert.Equal(new[] { episodes[0] }, await db.WorkoutInjuryEpisodes.AsNoTracking()
            .Select(row => row.Id).ToArrayAsync());
        Xunit.Assert.Equal(new[] { prescriptions[0] }, await db.WorkoutInjuryPrescriptionLinks.AsNoTracking()
            .Select(row => row.WorkoutDayExerciseId).ToArrayAsync());
        await SyntheticTenantSession.SetAsync(db, owners[1]);
        Xunit.Assert.Equal(new[] { episodes[1] }, await db.WorkoutInjuryEpisodes.AsNoTracking()
            .Select(row => row.Id).ToArrayAsync());
        Xunit.Assert.Equal(new[] { prescriptions[1] }, await db.WorkoutInjuryPrescriptionLinks.AsNoTracking()
            .Select(row => row.WorkoutDayExerciseId).ToArrayAsync());
        await transaction.CreateSavepointAsync("cross_tenant_link");
        var badId = Guid.NewGuid();
        var exception = await Xunit.Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO app.workout_injury_prescription_links (id, user_id, workout_day_exercise_id, injury_episode_id, original_exercise_id) VALUES ({badId}, {owners[1]}, {prescriptions[1]}, {episodes[0]}, {exerciseId})"));
        Xunit.Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        await transaction.RollbackToSavepointAsync("cross_tenant_link");
        await transaction.RollbackAsync();
    }

    private static PostgresDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection), $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
    }

    private static void AddUser(PostgresDbContext db, Guid owner, DateTime now) => db.Users.Add(new UserRecord
    {
        Id = owner, Email = $"episode-{owner:N}@example.invalid", Username = $"epi_{owner.ToString("N")[..10]}",
        CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now,
        IsOnboardingComplete = true
    });
}
