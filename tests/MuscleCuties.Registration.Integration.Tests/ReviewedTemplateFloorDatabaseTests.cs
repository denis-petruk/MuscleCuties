using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class ReviewedTemplateFloorDatabaseTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DatabasePrefix = "mc_test_";
    private const int InitialMinimum = 1;
    private const int UpdatedMinimum = 2;
    private const int Sha256HexLength = 64;
    private static readonly DateOnly SyntheticReviewDate = new(2026, 1, 1);

    [Xunit.Fact]
    public void ExplicitSyntheticFloorNeedsCompleteVersionedSlotsButNoAttribution()
    {
        var template = StrengthTemplateCatalog.Templates.First();
        var row = new ReviewedTemplateFloorRow { TemplateId = template.TemplateId, TemplateVersion = template.Version,
            MinimumWorkingSets = InitialMinimum, MinimumExercises = InitialMinimum,
            SlotFloorsJson = JsonSerializer.Serialize(template.Slots.ToDictionary(slot => slot.SlotId, _ => 0)) };
        Xunit.Assert.True(ReviewedTemplateFloorSource.Map([row]).ContainsKey(template.TemplateId));
        row.TemplateVersion = "synthetic-wrong-version";
        Xunit.Assert.Empty(ReviewedTemplateFloorSource.Map([row]));
        row.TemplateVersion = template.Version;
        row.SlotFloorsJson = "{}";
        Xunit.Assert.Empty(ReviewedTemplateFloorSource.Map([row]));
    }

    [Xunit.Fact]
    public async Task FloorLoadChangesFingerprintAndAppRoleCannotWrite()
    {
        var options = CreateOptions();
        await using var db = new PostgresDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var template = $"synthetic-{Guid.NewGuid():N}";
        var digest = new string('a', Sha256HexLength);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO catalog.workout_template_volume_floors
            (template_id, template_version, minimum_working_sets, minimum_exercises, slot_floors,
             reviewer, evidence_reference, evidence_sha256, reviewed_on)
            VALUES ({template}, 'synthetic-version', {InitialMinimum}, {InitialMinimum}, jsonb_build_object(),
                'synthetic-reviewer', 'synthetic-evidence', {digest}, {SyntheticReviewDate})
            """);
        var initial = await ReviewedTemplateFloorSource.LoadAsync(db, CancellationToken.None);
        var row = Xunit.Assert.Single(initial.Where(item => item.TemplateId == template));
        Xunit.Assert.Equal(InitialMinimum, row.MinimumWorkingSets);
        var firstFingerprint = PlanningCatalogFingerprint.FromInputs([], [], [], initial);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE catalog.workout_template_volume_floors SET minimum_working_sets = {UpdatedMinimum}
            WHERE template_id = {template}
            """);
        var updated = await ReviewedTemplateFloorSource.LoadAsync(db, CancellationToken.None);
        Xunit.Assert.NotEqual(firstFingerprint, PlanningCatalogFingerprint.FromInputs([], [], [], updated));
        Xunit.Assert.Equal(UpdatedMinimum, Xunit.Assert.Single(updated.Where(item => item.TemplateId == template)).MinimumWorkingSets);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL ROLE musclecuties_app");
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = new NpgsqlCommand("""
            SELECT has_table_privilege(current_user, 'catalog.workout_template_volume_floors', 'SELECT'),
                   has_table_privilege(current_user, 'catalog.workout_template_volume_floors', 'INSERT'),
                   has_table_privilege(current_user, 'catalog.workout_template_volume_floors', 'UPDATE'),
                   has_table_privilege(current_user, 'catalog.workout_template_volume_floors', 'DELETE'),
                   has_table_privilege(current_user, 'catalog.workout_template_volume_floors', 'TRUNCATE')
            """, connection);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Xunit.Assert.True(await reader.ReadAsync());
            Xunit.Assert.True(reader.GetBoolean(0));
            for (var index = 1; index < reader.FieldCount; index++) Xunit.Assert.False(reader.GetBoolean(index));
        }
        Xunit.Assert.Contains(await ReviewedTemplateFloorSource.LoadAsync(db, CancellationToken.None), item => item.TemplateId == template);
        await tx.RollbackAsync();
    }

    [Xunit.Fact]
    public async Task AppCatalogLockBlocksFloorWriterUntilTransactionEnds()
    {
        var options = CreateOptions();
        await using var reader = new PostgresDbContext(options);
        await using var writer = new PostgresDbContext(options);
        await using var readTransaction = await reader.Database.BeginTransactionAsync();
        await reader.Database.ExecuteSqlRawAsync("SET LOCAL ROLE musclecuties_app");
        await PlanningCatalogFingerprint.LockCatalogAsync(reader, CancellationToken.None);
        await using var writeTransaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '250ms'");
        var template = $"synthetic-{Guid.NewGuid():N}";
        var digest = new string('a', Sha256HexLength);
        var exception = await Xunit.Assert.ThrowsAsync<PostgresException>(() => writer.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO catalog.workout_template_volume_floors
            (template_id, template_version, minimum_working_sets, minimum_exercises, slot_floors,
             reviewer, evidence_reference, evidence_sha256, reviewed_on)
            VALUES ({template}, 'synthetic-version', {InitialMinimum}, {InitialMinimum}, jsonb_build_object(),
                'synthetic-reviewer', 'synthetic-evidence', {digest}, {SyntheticReviewDate})
            """));
        Xunit.Assert.Equal(PostgresErrorCodes.LockNotAvailable, exception.SqlState);
        await writeTransaction.RollbackAsync();
        await readTransaction.RollbackAsync();
    }

    private static DbContextOptions<PostgresDbContext> CreateOptions()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection), $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
    }
}

