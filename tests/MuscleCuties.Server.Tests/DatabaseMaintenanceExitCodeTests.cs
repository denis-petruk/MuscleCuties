using MuscleCuties.DbMaintenance;
using Npgsql;
using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// Pins how the maintenance CLI fails. The release preflight turns any non-zero <c>dbcheck</c> into a message,
/// so a database that is merely unreachable must not share the exit code of an incompatible schema: doing so
/// sent every beta deploy to the migration workflow for what was a connectivity problem.
/// </summary>
public sealed class DatabaseMaintenanceExitCodeTests
{
    private const string ConnectionStringVariable = "ConnectionStrings__PostgreSql";
    private const string LambdaFunctionVariable = "AWS_LAMBDA_FUNCTION_NAME";
    private const int UnreachableExitCode = 3;
    private const string SyntheticHost = "beta.example.rds.amazonaws.com";

    // One console and one environment at a time: both are process-wide.
    private static readonly Lock Gate = new();

    [Fact]
    public void AnUnreachableDatabaseExitsThreeAndSaysItIsNotDrift()
    {
        var result = RunDbCheck("Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic;Timeout=2");

        Assert.Equal(UnreachableExitCode, result.ExitCode);
        Assert.Contains("\"status\":\"unreachable\"", result.Output);
        Assert.Contains("cause: transport", result.Error);
        Assert.Contains("This is not schema drift.", result.Error);
    }

    [Fact]
    public void ASessionLockRunnerThatCannotOpenRelatedSessionExitsThree()
    {
        // The migration workflow wraps every deployment in this runner, so it needs the same signal as dbcheck.
        var result = Run("session-lock-runner", "--", "echo", "child",
            "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic;Timeout=2");

        Assert.Equal(UnreachableExitCode, result.ExitCode);
        Assert.Contains("\"status\":\"unreachable\"", result.Output);
        Assert.Contains("cause: transport", result.Error);
        Assert.DoesNotContain("child", result.Output);
    }

    [Fact]
    public void AMalformedConnectionStringIsAConfigurationFailureNotDrift()
    {
        // Parsing fails before any host is contacted, so nothing about the schema is known either.
        var result = RunDbCheck("Host=127.0.0.1;Port=not-a-port;Database=synthetic;Username=synthetic;Password=synthetic");

        Assert.Equal(UnreachableExitCode, result.ExitCode);
        Assert.Contains("\"status\":\"unreachable\"", result.Output);
        Assert.Contains("cause: configuration", result.Error);
        Assert.DoesNotContain("not-a-port", result.Output);
        Assert.DoesNotContain("not-a-port", result.Error);
    }

    [Fact]
    public void AServerThatAnsweredIsNeverClassifiedAsUnreachable()
    {
        Assert.False(DatabaseMaintenance.TryClassifyConnectionFailure(
            new PostgresException("relation does not exist", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable),
            out _));

        Assert.True(DatabaseMaintenance.TryClassifyConnectionFailure(
            new PostgresException("password authentication failed", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword),
            out var authentication));
        Assert.Equal("authentication", authentication);

        Assert.True(DatabaseMaintenance.TryClassifyConnectionFailure(
            new DatabaseConfigurationException("Database maintenance requires SSL Mode=VerifyFull."),
            out var configuration));
        Assert.Equal("configuration", configuration);
    }

    [Fact]
    public void ThePackagedRootBundleIsResolvedWhenTheConnectionDoesNotNameOne()
    {
        // The runner resolves the RDS chain from the bundle shipped next to the binary; without this the
        // migration workflow failed the handshake while dbcheck from a repository checkout passed.
        var connection = new NpgsqlConnectionStringBuilder(
            $"Host={SyntheticHost};Database=synthetic;Username=synthetic;Password=synthetic;SSL Mode=VerifyFull");

        DatabaseMaintenance.ApplyRootCertificateFallback(connection);

        Assert.False(string.IsNullOrWhiteSpace(connection.RootCertificate));
        Assert.True(File.Exists(connection.RootCertificate), connection.RootCertificate);
        Assert.EndsWith(Path.Combine("certs", "global-bundle.pem"), connection.RootCertificate);
    }

    [Fact]
    public void ANonLoopbackConnectionWithoutVerifyFullIsAConfigurationFailure()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            $"Host={SyntheticHost};Database=synthetic;Username=synthetic;Password=synthetic");

        Assert.Throws<DatabaseConfigurationException>(() =>
            DatabaseMaintenance.ApplyRootCertificateFallback(connection));
    }

    [Fact]
    public void ALoopbackConnectionIsNeverGivenTheRemoteBundle()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            "Host=127.0.0.1;Database=synthetic;Username=synthetic;Password=synthetic");

        DatabaseMaintenance.ApplyRootCertificateFallback(connection);

        Assert.True(string.IsNullOrEmpty(connection.RootCertificate));
    }

    private static (int ExitCode, string Output, string Error) RunDbCheck(string connectionString) =>
        Run("dbcheck", connectionString);

    private static (int ExitCode, string Output, string Error) Run(string first, params string[] rest)
    {
        var connectionString = rest[^1];
        var args = new[] { first }.Concat(rest[..^1]).ToArray();
        return RunAsync(args, connectionString);
    }

    private static (int ExitCode, string Output, string Error) RunAsync(string[] args, string connectionString)
    {
        lock (Gate)
        {
            var previousConnection = Environment.GetEnvironmentVariable(ConnectionStringVariable);
            var previousLambda = Environment.GetEnvironmentVariable(LambdaFunctionVariable);
            var previousOut = Console.Out;
            var previousError = Console.Error;
            var output = new StringWriter();
            var error = new StringWriter();
            Environment.SetEnvironmentVariable(ConnectionStringVariable, connectionString);
            Environment.SetEnvironmentVariable(LambdaFunctionVariable, null);
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                var exitCode = DatabaseMaintenance.RunAsync(args).GetAwaiter().GetResult();
                return (exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(previousOut);
                Console.SetError(previousError);
                Environment.SetEnvironmentVariable(ConnectionStringVariable, previousConnection);
                Environment.SetEnvironmentVariable(LambdaFunctionVariable, previousLambda);
            }
        }
    }
}
