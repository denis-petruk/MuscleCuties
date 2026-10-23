using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PostgreSqlTransientRetryIntegrationTests
{
    private const string ConnectionVariable = "MC_RETRY_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DisposableDatabaseName = "mc_retry_disposable";
    private const string CreateProbeTableSql =
        "CREATE TABLE IF NOT EXISTS public.mc_retry_probe (id uuid PRIMARY KEY, attempt_number integer NOT NULL)";
    private const string TerminateCurrentBackendSql = "SELECT pg_terminate_backend(pg_backend_pid())";
    private const string ReadProbeSql = "SELECT attempt_number FROM public.mc_retry_probe WHERE id = @id";
    private const string DeleteProbeSql = "DELETE FROM public.mc_retry_probe WHERE id = @id";
    private const int ExpectedAttemptCount = 2;

    [Fact]
    public async Task ExplicitTransactionRetriesAfterPostgreSqlDropsItsConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL database.");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(LocalHost, connection.Host);
        Assert.Equal(DisposableDatabaseName, connection.Database);

        var probeId = Guid.CreateVersion7();
        await using var setupConnection = new NpgsqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await using (var createTable = new NpgsqlCommand(CreateProbeTableSql, setupConnection))
            await createTable.ExecuteNonQueryAsync();

        await using var services = new ServiceCollection()
            .AddPostgreSqlPersistence(connectionString)
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var attempts = 0;

        try
        {
            await database.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                attempts++;
                database.ChangeTracker.Clear();
                await using var transaction = await database.Database.BeginTransactionAsync();
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO public.mc_retry_probe (id, attempt_number) VALUES ({probeId}, {attempts})");

                if (attempts == 1)
                    await database.Database.ExecuteSqlRawAsync(TerminateCurrentBackendSql);

                await transaction.CommitAsync();
            });

            Assert.Equal(ExpectedAttemptCount, attempts);
            await using var readProbe = new NpgsqlCommand(ReadProbeSql, setupConnection);
            readProbe.Parameters.AddWithValue("id", probeId);
            Assert.Equal(ExpectedAttemptCount, await readProbe.ExecuteScalarAsync());
        }
        finally
        {
            await using var deleteProbe = new NpgsqlCommand(DeleteProbeSql, setupConnection);
            deleteProbe.Parameters.AddWithValue("id", probeId);
            await deleteProbe.ExecuteNonQueryAsync();
        }
    }
}
