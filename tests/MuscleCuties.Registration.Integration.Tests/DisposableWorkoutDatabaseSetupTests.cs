using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>Explicit local-only preparation; never included in the normal integration suite.</summary>
public sealed class DisposableWorkoutDatabaseSetupTests
{
    private const string SetupVariable = "MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE";
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string DatabasePrefix = "mc_test_";
    private const string LoopbackHost = "127.0.0.1";

    [Xunit.Fact]
    public async Task InitializeAndSeedExplicitlySelectedDisposableDatabase()
    {
        if (Environment.GetEnvironmentVariable(SetupVariable) != bool.TrueString) return;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
        Xunit.Assert.Equal(LoopbackHost, connection.Host);
        Xunit.Assert.StartsWith(DatabasePrefix, connection.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);

        var bootstrap = await PostgresSchemaBootstrap.InitializeAsync(db, seed: true);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(bootstrap.ModelFingerprint));
        Xunit.Assert.True(bootstrap.Seed is { TotalManifestRows: > 0 });

        var reviewPath = Path.Combine(FindRoot(), "src", "MuscleCuties.Infrastructure.PostgreSql", "Workout",
            "Planning", "ReviewData", "ReviewedExerciseCatalogV020.json");
        var importer = new PostgresReviewedExerciseCatalogImporterV020(db);
        var review = await importer.DryRunAsync(reviewPath);
        Xunit.Assert.True(review.CanImport, string.Join("; ", review.Issues));
        await importer.ApplyAsync(reviewPath);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
