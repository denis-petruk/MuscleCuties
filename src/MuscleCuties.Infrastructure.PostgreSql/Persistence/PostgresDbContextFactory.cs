using Microsoft.EntityFrameworkCore.Design;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>The design-time factory, so `dotnet ef` can build the context without starting the server.</summary>
public sealed class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "MUSCLECUTIES_POSTGRESQL_MIGRATIONS_CONNECTION";

    /// <summary>Builds the context from the environment's connection string; design-time use only, it never opens a connection by itself.</summary>
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
