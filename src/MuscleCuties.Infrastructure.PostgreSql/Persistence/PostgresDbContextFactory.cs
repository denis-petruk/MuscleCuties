using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public sealed class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "MUSCLECUTIES_POSTGRESQL_MIGRATIONS_CONNECTION";

    public PostgresDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} to a local development database connection string before running EF design-time commands.");

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString, provider => provider
                .MigrationsAssembly(typeof(PostgresDbContext).Assembly.GetName().Name)
                .CommandTimeout(PostgreSqlTimeoutDefaults.CommandSeconds))
            .Options;

        return new PostgresDbContext(options);
    }
}
