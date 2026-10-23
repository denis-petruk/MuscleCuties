using Npgsql;

namespace MuscleCuties.Server;

public sealed record LambdaRdsProxyPoolOptions(
    int MinPoolSize,
    int MaxPoolSize,
    int ConnectionIdleLifetimeSeconds,
    int ConnectionTimeoutSeconds,
    int CommandTimeoutSeconds,
    bool RequireSslMode,
    bool RunMigrationsOnColdStart);

/// <summary>
/// PG10-02: AWS Lambda (.NET 10) hosting &amp; RDS Proxy connection-pool configuration helper.
/// Ensures bounded Npgsql connection pooling for Lambda concurrency, enforces TLS when targeting
/// RDS Proxy, and strictly forbids automatic schema migrations or seed ingestion on Lambda cold start.
/// </summary>
public static class LambdaHostingConfiguration
{
    private const string LambdaFunctionNameEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";
    public const int DefaultLambdaMaxPoolSize = 5;
    public const int DefaultLambdaMinPoolSize = 0;
    public const int DefaultConnectionIdleLifetimeSeconds = 60;
    public const int DefaultConnectionTimeoutSeconds = 10;
    public const int DefaultCommandTimeoutSeconds = 20;

    public static bool IsRunningInLambda() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LambdaFunctionNameEnvironmentVariable));

    public static LambdaRdsProxyPoolOptions ResolveOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("Lambda:DatabasePool");
        var maxPoolSize = section.GetValue("MaxPoolSize", DefaultLambdaMaxPoolSize);
        var minPoolSize = section.GetValue("MinPoolSize", DefaultLambdaMinPoolSize);
        var idleLifetime = section.GetValue("ConnectionIdleLifetimeSeconds", DefaultConnectionIdleLifetimeSeconds);
        var connectionTimeout = section.GetValue("ConnectionTimeoutSeconds", DefaultConnectionTimeoutSeconds);
        var commandTimeout = section.GetValue("CommandTimeoutSeconds", DefaultCommandTimeoutSeconds);
        var requireSsl = section.GetValue("RequireSslMode", true);
        var runMigrationsOnColdStart = section.GetValue("RunMigrationsOnColdStart", false);

        if (runMigrationsOnColdStart)
        {
            throw new InvalidOperationException(
                "Schema migrations and canonical seed ingestion must never execute on AWS Lambda cold start (PG10-02/PG10-04).");
        }

        if (maxPoolSize <= 0 || maxPoolSize > 25 || minPoolSize < 0 || minPoolSize > maxPoolSize)
        {
            throw new InvalidOperationException(
                "Lambda:DatabasePool MinPoolSize/MaxPoolSize must be bounded between 0 and 25 for RDS Proxy safety.");
        }

        if (!requireSsl)
            throw new InvalidOperationException("Lambda RDS Proxy connections require full TLS certificate verification.");

        if (idleLifetime <= 0 || connectionTimeout <= 0 || commandTimeout <= 0)
            throw new InvalidOperationException("Lambda database timeouts and idle lifetime must be positive.");

        return new LambdaRdsProxyPoolOptions(
            minPoolSize,
            maxPoolSize,
            idleLifetime,
            connectionTimeout,
            commandTimeout,
            requireSsl,
            RunMigrationsOnColdStart: false);
    }

    public static string BuildBoundedRdsProxyConnectionString(
        string baseConnectionString,
        LambdaRdsProxyPoolOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseConnectionString);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Pooling = true,
            MinPoolSize = options.MinPoolSize,
            MaxPoolSize = options.MaxPoolSize,
            ConnectionIdleLifetime = options.ConnectionIdleLifetimeSeconds,
            Timeout = options.ConnectionTimeoutSeconds,
            CommandTimeout = options.CommandTimeoutSeconds,
            NoResetOnClose = false
        };

        if (options.RequireSslMode)
        {
            builder.SslMode = SslMode.VerifyFull;
        }

        return builder.ConnectionString;
    }
}
