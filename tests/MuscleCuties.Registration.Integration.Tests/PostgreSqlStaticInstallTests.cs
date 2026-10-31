using System.Diagnostics;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PostgreSqlStaticInstallTests
{
    [DockerBootstrapFact]
    public async Task FreshDatabaseInstallsWithoutEfMigrations()
    {
        // This test creates its own disposable PostgreSQL cluster. It never accepts an existing connection.
        var container = await DockerAsync("run", "--detach", "--rm", "--pull=never",
            "--publish", "127.0.0.1::5432", "--env", "POSTGRES_PASSWORD=disposable-fixture-only",
            "--env", "POSTGRES_DB=mc_bootstrap_test_fixture", "postgres:17-alpine");
        try
        {
            var endpoint = (await DockerAsync("port", container, "5432/tcp")).Trim();
            Assert.StartsWith("127.0.0.1:", endpoint, StringComparison.Ordinal);
            var port = int.Parse(endpoint["127.0.0.1:".Length..], System.Globalization.CultureInfo.InvariantCulture);
            var adminBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = port, Database = "mc_bootstrap_test_fixture",
                Username = "postgres", Password = "disposable-fixture-only", Pooling = false,
                Timeout = 2
            };
            await using var admin = await OpenWhenReadyAsync(adminBuilder.ConnectionString);

            await DockerAsync("cp", RepositoryPath("services", "postgresql"), $"{container}:/tmp/pg");
            await DockerAsync("exec", container, "psql", "-U", "postgres",
                "-d", "mc_bootstrap_test_fixture", "-v", "ON_ERROR_STOP=1", "-f", "/tmp/pg/install.sql");

            Assert.Equal(104L, await LongAsync(admin, "SELECT count(*) FROM catalog.exercises"));
            Assert.Equal(6L, await LongAsync(admin, "SELECT count(*) FROM catalog.week_templates"));
            Assert.Equal(60L, await LongAsync(admin, "SELECT count(*) FROM catalog.workout_exercise_definitions"));
            Assert.False(await BooleanAsync(admin, """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory')
                """));

            Assert.True(await BooleanAsync(admin, "SELECT has_table_privilege('musclecuties_app','app.users','SELECT')"));
            Assert.False(await BooleanAsync(admin, "SELECT has_table_privilege('musclecuties_app','app.photo_object_reconcile_targets','SELECT')"));
            Assert.False(await BooleanAsync(admin, "SELECT has_schema_privilege('musclecuties_migrator','public','CREATE')"));
            Assert.True(await BooleanAsync(admin, """
                SELECT pg_get_userbyid(c.relowner) = 'musclecuties_photo_reconcile_owner'
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'app' AND c.relname = 'photo_object_reconcile_targets'
                """));
        }
        finally
        {
            await DockerAsync("rm", "--force", container);
        }
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }

    private static async Task<NpgsqlConnection> OpenWhenReadyAsync(string connectionString)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (NpgsqlException) when (DateTime.UtcNow < deadline)
            {
                await connection.DisposeAsync();
                await Task.Delay(500);
            }
        }
    }

    private static async Task<string> DockerAsync(params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        }};
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"Docker failed: {await error}");
        return (await output).Trim();
    }

    private static async Task<bool> BooleanAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> LongAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class DockerBootstrapFactAttribute : FactAttribute
{
    public DockerBootstrapFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MC_BOOTSTRAP_DOCKER_TEST") != "1")
            Skip = "Set MC_BOOTSTRAP_DOCKER_TEST=1 to run the disposable PostgreSQL container test.";
    }
}
