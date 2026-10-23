using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public static class PostgreSqlServiceCollectionExtensions
{
    private const int MaximumTransientRetryCount = 3;
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddPostgreSqlPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!connectionBuilder.ContainsKey("Timeout"))
            connectionBuilder.Timeout = PostgreSqlTimeoutDefaults.ConnectionSeconds;
        if (!connectionBuilder.ContainsKey("Command Timeout"))
            connectionBuilder.CommandTimeout = PostgreSqlTimeoutDefaults.CommandSeconds;

        if (connectionBuilder.SslMode == SslMode.VerifyFull && string.IsNullOrWhiteSpace(connectionBuilder.RootCertificate))
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "certs", "global-bundle.pem"),
                Path.Combine(AppContext.BaseDirectory, "certs", "global-bundle.pem"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "certs", "global-bundle.pem")
            };
            var found = candidates.FirstOrDefault(File.Exists);
            if (found is not null)
                connectionBuilder.RootCertificate = found;
        }

        services.AddDbContextPool<PostgresDbContext>(options => options
            .UseNpgsql(connectionBuilder.ConnectionString, provider => provider
                .MigrationsAssembly(typeof(PostgresDbContext).Assembly.GetName().Name)
                .CommandTimeout(connectionBuilder.CommandTimeout)
                .EnableRetryOnFailure(MaximumTransientRetryCount, MaximumRetryDelay, null)));

        return services;
    }
}

public static class PostgreSqlTimeoutDefaults
{
    public const int ConnectionSeconds = 15;
    public const int CommandSeconds = 30;
}
