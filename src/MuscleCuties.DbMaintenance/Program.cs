using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MuscleCuties.DbMaintenance;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

return await DatabaseMaintenance.RunAsync(args);

/// <summary>
///     Command-line entry points for schema maintenance: <c>dbinit</c>, <c>migrate</c>, <c>dbcheck</c>, <c>seed</c>,
///     <c>baseline-script</c>, <c>session-lock-runner</c>, and the photo cleanup pass. Runs as a process or a
///     deployment step, never inside Lambda, and prints JSON results for the caller to read.
/// </summary>
public static class DatabaseMaintenance
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__PostgreSql";
    private const string CleanupDispatchConnectionStringEnvironmentVariable = "ConnectionStrings__PhotoCleanupDispatch";
    private const string CleanupProcessorConnectionStringEnvironmentVariable = "ConnectionStrings__PhotoCleanupProcessor";
    private const string LambdaFunctionEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";
    private const string DbInitCommand = "dbinit";
    private const string DbCheckCommand = "dbcheck";
    private const string MigrateCommand = "migrate";
    private const string SeedCommand = "seed";
    private const string BaselineScriptCommand = "baseline-script";
    private const string SessionLockRunnerCommand = "session-lock-runner";
    private const string CleanupPhotosCommand = "cleanup-photos";
    private const string CompatibleStatus = "compatible";
    private const string IncompatibleStatus = "incompatible";
    private const string UnreachableStatus = "unreachable";
    private const string ContractVersion = "1.0.0";
    private const int FailureExitCode = 1;
    private const int UnreachableExitCode = 3;
    private const string UnreachableHint =
        "No session was established, so the schema was never compared: check the runner's network path to the " +
        "endpoint, the trusted root certificate (certs/global-bundle.pem), the connection string and the role. " +
        "This is not schema drift.";
    private const int AdvisoryLockAcquireTimeoutSeconds = 5;
    private const int AdvisoryLockPollMilliseconds = 100;
    private const int CleanupPageSizeMinimum = 1;
    private const int CleanupPageSizeMaximum = 100;
    private const int CleanupPageCountMinimum = 1;
    private const int CleanupPageCountMaximum = 20;
    private const int CleanupOwnerBatchMinimum = 1;
    private const int CleanupOwnerBatchMaximum = 100;
    private const int CleanupPageSizeArgumentIndex = 1;
    private const int CleanupPageCountArgumentIndex = 2;
    private const int CleanupOwnerBatchArgumentIndex = 3;
    private const int CleanupLeaseMinutesArgumentIndex = 4;
    private const int MaximumCleanupLeaseMinutes = 1440;
    private const int InvalidUsageExitCode = 2;
    private const int LockHealthPollIntervalSeconds = 1;
    private const int LockHealthCommandTimeoutSeconds = 2;
    private const int LockChildShutdownTimeoutSeconds = 10;

    /// <summary>
    ///     Dispatches one command and returns the process exit code: 0 on success, 1 on a failed operation or an
    ///     incompatible schema, 2 on invalid usage, 3 when no database session could be established. Refuses to
    ///     run inside AWS Lambda and keeps provider detail out of the output.
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var command))
        {
            Console.Error.WriteLine(
                "Usage: DbMaintenance migrate | dbinit | dbcheck | seed | baseline-script | session-lock-runner -- <child-command> [args...] | " +
                "cleanup-photos <page-size> <page-count> <owner-batch-size> <lease-minutes>");
            return InvalidUsageExitCode;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LambdaFunctionEnvironmentVariable)))
        {
            Console.Error.WriteLine("Database maintenance is forbidden in AWS Lambda.");
            return InvalidUsageExitCode;
        }

        if (command == BaselineScriptCommand)
        {
            // Maintainer-only: emit the model DDL used to regenerate the frozen baseline
            // migration. It reads the EF model and does not connect to a database.
            var options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=mc_baseline_generator;Username=baseline;Password=baseline")
                .Options;
            await using var baselineContext = new PostgresDbContext(options);
            Console.Write(baselineContext.Database.GenerateCreateScript());
            return 0;
        }

        if (command == SessionLockRunnerCommand)
        {
            try
            {
                return await RunSessionLockRunnerAsync(args);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (TryClassifyConnectionFailure(exception, out var cause))
                {
                    WriteJson(new
                    {
                        contract_version = ContractVersion,
                        status = UnreachableStatus,
                        command = SessionLockRunnerCommand,
                        failure = cause
                    });
                    Console.Error.WriteLine($"Session lock runner could not open a database session ({exception.GetType().Name}; cause: {cause}). {UnreachableHint}");
                    return UnreachableExitCode;
                }

                Console.Error.WriteLine($"Session lock runner failed ({exception.GetType().Name}).");
                return FailureExitCode;
            }
        }

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        var isCleanupCommand = command == CleanupPhotosCommand;
        if (!isCleanupCommand && string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine($"ConnectionStrings__PostgreSql is required in the protected runner environment.");
            return InvalidUsageExitCode;
        }

        try
        {
            if (isCleanupCommand)
                return await RunPhotoCleanupAsync(args);

            var connection = new NpgsqlConnectionStringBuilder(connectionString);
            ApplyRootCertificateFallback(connection);

            var options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(connection.ConnectionString)
                .Options;
            await using var context = new PostgresDbContext(options);

            if (command == DbInitCommand)
            {
                var result = await PostgresSchemaBootstrap.InitializeAsync(context, seed: true);
                WriteJson(new
                {
                    contract_version = ContractVersion,
                    status = CompatibleStatus,
                    created_from_model = result.CreatedFromModel,
                    security_statements_applied = result.SecurityStatementCount,
                    schema_fingerprint_sha256 = result.ModelFingerprint,
                    seeded_rows = result.Seed?.TotalManifestRows ?? 0
                });
                return 0;
            }

            if (command == MigrateCommand)
            {
                var summary = await PostgresSchemaMigrations.ApplyMigrationsAsync(context);
                WriteJson(new
                {
                    contract_version = ContractVersion,
                    status = CompatibleStatus,
                    schema_version = summary.CurrentVersion,
                    migrations_applied = summary.MigrationsApplied,
                    details = summary.Details.Select(d => new
                    {
                        version = d.Version,
                        script_name = d.ScriptName,
                        checksum_sha256 = d.ChecksumSha256,
                        already_applied = d.AlreadyApplied
                    })
                });
                return 0;
            }

            if (command == SeedCommand)
            {
                var summary = await PostgresCanonicalCatalogSeeder.IngestEmbeddedManifestAsync(context);
                WriteJson(new
                {
                    contract_version = ContractVersion,
                    status = CompatibleStatus,
                    seeded_rows = summary.TotalManifestRows
                });
                return 0;
            }

            var report = await PostgresSchemaDriftInspector.InspectAsync(context);
            WriteJson(new
            {
                contract_version = ContractVersion,
                status = report.IsCompatible ? CompatibleStatus : IncompatibleStatus,
                schema_fingerprint_sha256 = report.ModelFingerprint,
                recorded_fingerprint_sha256 = report.RecordedFingerprint,
                model_table_count = report.ModelTableCount,
                policy_count = report.PolicyCount,
                failure_codes = report.FailureCodes,
                details = report.Details
            });
            return report.IsCompatible ? 0 : 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failure to open a session says nothing about the schema. Reporting it as a command failure made
            // the release preflight blame schema drift and send the operator to the migration workflow.
            if (TryClassifyConnectionFailure(exception, out var cause))
            {
                WriteJson(new
                {
                    contract_version = ContractVersion,
                    status = UnreachableStatus,
                    command,
                    failure = cause
                });
                Console.Error.WriteLine($"Database is unreachable ({exception.GetType().Name}; cause: {cause}). {UnreachableHint}");
                return UnreachableExitCode;
            }

            // Provider exceptions may include connection strings or object names. Keep runner logs secret-free.
            Console.Error.WriteLine($"Database maintenance failed ({exception.GetType().Name}). Check the runner's connection, certificate and role settings.");
            return FailureExitCode;
        }
    }

    /// <summary>
    ///     Runs one photo cleanup pass with the dedicated dispatch and processor connections, both of which
    ///     must use <c>SSL Mode=VerifyFull</c>. The dispatch cursor is persisted so the next run resumes.
    /// </summary>
    private static async Task<int> RunPhotoCleanupAsync(string[] args)
    {
        var dispatchConnectionString = Environment.GetEnvironmentVariable(CleanupDispatchConnectionStringEnvironmentVariable);
        var processorConnectionString = Environment.GetEnvironmentVariable(CleanupProcessorConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(dispatchConnectionString) || string.IsNullOrWhiteSpace(processorConnectionString))
            throw new InvalidOperationException("Dedicated photo cleanup dispatch and processor connection strings are required.");

        var dispatchBuilder = new NpgsqlConnectionStringBuilder(dispatchConnectionString);
        if (dispatchBuilder.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Photo cleanup connections require SSL Mode=VerifyFull.");
        var processorBuilder = new NpgsqlConnectionStringBuilder(processorConnectionString);
        if (processorBuilder.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Photo cleanup connections require SSL Mode=VerifyFull.");

        var pageSize = int.Parse(args[CleanupPageSizeArgumentIndex], System.Globalization.CultureInfo.InvariantCulture);
        var pageCount = int.Parse(args[CleanupPageCountArgumentIndex], System.Globalization.CultureInfo.InvariantCulture);
        var ownerBatchSize = int.Parse(args[CleanupOwnerBatchArgumentIndex], System.Globalization.CultureInfo.InvariantCulture);
        var leaseDuration = TimeSpan.FromMinutes(double.Parse(args[CleanupLeaseMinutesArgumentIndex], System.Globalization.CultureInfo.InvariantCulture));
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        await using var dispatchDataSource = NpgsqlDataSource.Create(dispatchBuilder.ConnectionString);
        using var httpClient = new HttpClient();
        var s3Cleanup = new DefaultS3PhotoObjectCleanup(httpClient, configuration);
        var discovery = new PostgresDuePhotoCleanupOwnerDiscovery(dispatchDataSource);
        var cursorStore = new PostgresPhotoCleanupDispatchCursor(dispatchDataSource);
        await using var cursorLease = await cursorStore.AcquireAsync();
        var cursor = await cursorLease.LoadAsync();
        var runner = new LocalPhotoCleanupOwnerProcessorRunner(processorBuilder.ConnectionString, s3Cleanup, configuration);
        var dispatcher = new PhotoCleanupDispatcher(discovery, runner, TimeProvider.System);
        var result = await dispatcher.DispatchAsync(
            pageSize, pageCount, ownerBatchSize, leaseDuration,
            initialAfterOwnerId: cursor,
            persistCursor: cursorLease.SaveAsync);
        Console.WriteLine($"Photo cleanup pass: {result.Pages} pages, {result.OwnersProcessed} owners processed, {result.OwnersFailed} owner failures, {result.Claimed} items claimed, {result.Completed} completed, {result.Retried} retryable, {result.Fenced} fenced.");
        return result.OwnersFailed == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Recognises the command in the first argument. <c>session-lock-runner</c> is only accepted when the
    ///     child command follows a <c>--</c> separator; anything else is rejected as invalid usage.
    /// </summary>
    public static bool TryParse(string[] args, out string? command)
    {
        command = null;

        if (args.Length > 0 && args[0] == SessionLockRunnerCommand)
        {
            var dashIndex = Array.IndexOf(args, "--");
            if (dashIndex != -1 && dashIndex + 1 < args.Length)
            {
                command = SessionLockRunnerCommand;
                return true;
            }

            return false;
        }

        if (args is [MigrateCommand] or [DbInitCommand] or [DbCheckCommand] or [SeedCommand] or [BaselineScriptCommand])
        {
            command = args[0];
            return true;
        }

        if (args is [CleanupPhotosCommand, var pageSize, var pageCount, var ownerBatch, var leaseMinutes] &&
            int.TryParse(pageSize, out var parsedPageSize) && parsedPageSize is >= CleanupPageSizeMinimum and <= CleanupPageSizeMaximum &&
            int.TryParse(pageCount, out var parsedPageCount) && parsedPageCount is >= CleanupPageCountMinimum and <= CleanupPageCountMaximum &&
            int.TryParse(ownerBatch, out var parsedOwnerBatch) && parsedOwnerBatch is >= CleanupOwnerBatchMinimum and <= CleanupOwnerBatchMaximum &&
            double.TryParse(leaseMinutes, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLease) &&
            parsedLease > 0 && parsedLease <= MaximumCleanupLeaseMinutes)
        {
            command = CleanupPhotosCommand;
            return true;
        }

        return false;
    }

    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Writes one maintenance result as snake_case JSON for the calling tool.</summary>
    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, ResultJsonOptions));

    /// <summary>
    ///     Holds the deployment advisory lock for the lifetime of a child command, so two deployments cannot
    ///     migrate the same database at once. The lock is verified on the child's own backend before it starts.
    /// </summary>
    private static async Task<int> RunSessionLockRunnerAsync(string[] args)
    {
        var dashIndex = Array.IndexOf(args, "--");
        var childCmd = args[dashIndex + 1];
        var childArgs = args[(dashIndex + 2)..];

        if (Environment.GetEnvironmentVariable("MC_RELEASE_OFFLINE_TEST") == "1")
        {
            return await ExecuteChildProcessAsync(childCmd, childArgs);
        }

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine($"ConnectionStrings__PostgreSql is required for {SessionLockRunnerCommand}.");
            return 1;
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        // This runner opens its own connection, so it needs the same trusted-root fallback as the command
        // path; without it the migration workflow failed on the RDS chain while dbcheck passed.
        ApplyRootCertificateFallback(connection);

        await using var npgsqlConnection = new NpgsqlConnection(connection.ConnectionString);
        await npgsqlConnection.OpenAsync();

        if (!await TryAcquireAdvisoryLockAsync(npgsqlConnection))
        {
            Console.Error.WriteLine($"Failed to acquire deployment advisory lock within {AdvisoryLockAcquireTimeoutSeconds} seconds.");
            return 1;
        }

        var backendPid = npgsqlConnection.ProcessID;
        if (!await HasSessionLockAsync(npgsqlConnection, backendPid))
        {
            Console.Error.WriteLine("Deployment advisory lock could not be verified before starting the child.");
            return 1;
        }

        return await ExecuteChildProcessAsync(childCmd, childArgs, npgsqlConnection, backendPid);
    }

    /// <summary>
    ///     Applies the checks and the trusted-root fallback every database connection needs before it opens:
    ///     a non-loopback host requires <c>SSL Mode=VerifyFull</c>, and a missing or unreadable root certificate
    ///     resolves to the packaged RDS bundle rather than failing the handshake.
    /// </summary>
    private static void ApplyRootCertificateFallback(NpgsqlConnectionStringBuilder connection)
    {
        if (connection.Host is "127.0.0.1" or "localhost")
            return;

        if (connection.SslMode != SslMode.VerifyFull)
            throw new DatabaseConfigurationException("Database maintenance requires SSL Mode=VerifyFull.");

        var certificate = string.IsNullOrWhiteSpace(connection.RootCertificate)
            ? "certs/global-bundle.pem"
            : connection.RootCertificate;
        var foundCert = CertificateCandidates(certificate).FirstOrDefault(File.Exists);
        if (foundCert is not null)
            connection.RootCertificate = foundCert;
    }

    /// <summary>Where the packaged bundle may sit: the working directory, the output directory or the repository root.</summary>
    private static IEnumerable<string> CertificateCandidates(string relativePath) =>
    [
        Path.Combine(Directory.GetCurrentDirectory(), relativePath),
        Path.Combine(AppContext.BaseDirectory, relativePath),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath)
    ];

    /// <summary>
    ///     Decides whether a failure happened before a session existed, and names the cause in a secret-free
    ///     token. A server that answered is never a connection failure, however its reply is spelled.
    /// </summary>
    public static bool TryClassifyConnectionFailure(Exception exception, out string cause)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case DatabaseConfigurationException:
                    cause = "configuration";
                    return true;
                case PostgresException postgres:
                    switch (postgres.SqlState)
                    {
                        case PostgresErrorCodes.InvalidPassword:
                        case PostgresErrorCodes.InvalidAuthorizationSpecification:
                            cause = "authentication";
                            return true;
                        case PostgresErrorCodes.InvalidCatalogName:
                            cause = "database-missing";
                            return true;
                    }

                    // The server accepted the connection and answered: the failure is not a connectivity one.
                    cause = string.Empty;
                    return false;
                case AuthenticationException:
                    cause = "tls";
                    return true;
                case SocketException socket:
                    cause = socket.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                        ? "dns"
                        : "transport";
                    return true;
                case TimeoutException:
                    cause = "timeout";
                    return true;
            }
        }

        if (exception is NpgsqlException)
        {
            cause = "transport";
            return true;
        }

        cause = string.Empty;
        return false;
    }

    /// <summary>
    ///     Polls the deployment advisory lock until it is granted or the acquisition window closes. Returns
    ///     false rather than throwing when another deployment holds it.
    /// </summary>
    private static async Task<bool> TryAcquireAdvisoryLockAsync(NpgsqlConnection connection)
    {
        var deadline = DateTime.UtcNow.AddSeconds(AdvisoryLockAcquireTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lock_class, @lock_key)", connection);
            command.Parameters.AddWithValue("lock_class", DeploymentAdvisoryLock.ClassId);
            command.Parameters.AddWithValue("lock_key", DeploymentAdvisoryLock.KeyId);
            if (await command.ExecuteScalarAsync() is true)
                return true;

            await Task.Delay(AdvisoryLockPollMilliseconds);
        }

        return false;
    }

    /// <summary>Runs a child command, forwarding interrupts, and keeps the advisory lock connection alive until it exits.</summary>
    private static async Task<int> ExecuteChildProcessAsync(
        string childCmd,
        string[] childArgs,
        NpgsqlConnection? lockConnection = null,
        int backendPid = 0)
    {
        var psi = new ProcessStartInfo
        {
            FileName = childCmd,
            UseShellExecute = false
        };
        foreach (var arg in childArgs)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start child command: {childCmd}");

        ConsoleCancelEventHandler cancelHandler = (_, _) =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var childExit = process.WaitForExitAsync();
            if (lockConnection is not null)
            {
                while (!childExit.IsCompleted)
                {
                    var completed = await Task.WhenAny(childExit,
                        Task.Delay(TimeSpan.FromSeconds(LockHealthPollIntervalSeconds)));
                    if (completed == childExit)
                        break;

                    if (!await HasSessionLockAsync(lockConnection, backendPid))
                    {
                        Console.Error.WriteLine("Deployment advisory lock session lost; terminating the child.");
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                        try { await childExit.WaitAsync(TimeSpan.FromSeconds(LockChildShutdownTimeoutSeconds)); }
                        catch (TimeoutException) { }
                        return 1;
                    }
                }
            }

            await childExit;
            if (lockConnection is not null && !await HasSessionLockAsync(lockConnection, backendPid))
            {
                Console.Error.WriteLine("Deployment advisory lock session lost before the child result was accepted.");
                return 1;
            }
            return process.ExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    /// <summary>
    ///     Confirms this connection's backend still owns the session-level advisory lock. A failed or
    ///     timed-out probe returns false, because it cannot establish ownership.
    /// </summary>
    private static async Task<bool> HasSessionLockAsync(NpgsqlConnection connection, int backendPid)
    {
        try
        {
            await using var command = new NpgsqlCommand("""
                SELECT pg_backend_pid() = @backend_pid
                   AND EXISTS (
                       SELECT 1 FROM pg_locks
                       WHERE pid = pg_backend_pid()
                         AND locktype = 'advisory'
                         AND granted
                         AND classid::bigint = @lock_class
                         AND objid::bigint = @lock_key
                         AND objsubid = 2
                   )
                """, connection)
            {
                CommandTimeout = LockHealthCommandTimeoutSeconds
            };
            command.Parameters.AddWithValue("backend_pid", backendPid);
            command.Parameters.AddWithValue("lock_class", DeploymentAdvisoryLock.ClassId);
            command.Parameters.AddWithValue("lock_key", DeploymentAdvisoryLock.KeyId);
            return await command.ExecuteScalarAsync() is true;
        }
        catch (Exception)
        {
            // A failed or timed-out probe cannot establish continued lock ownership.
            return false;
        }
    }
}

/// <summary>
///     Runs the photo cleanup processor for one owner against the processor connection, which the
///     dispatcher hands in per owner batch.
/// </summary>
internal sealed class LocalPhotoCleanupOwnerProcessorRunner(
    string processorConnectionString,
    MuscleCuties.UseCases.Features.Profile.PhotoVault.IS3PhotoObjectCleanup s3Cleanup,
    IConfiguration configuration) : IPhotoCleanupOwnerProcessorRunner
{
    /// <summary>Claims and cleans one owner's due photo objects, bounded by the batch size and lease.</summary>
    public async Task<PhotoCleanupBatchResult> ProcessOwnerAsync(Guid ownerId, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(processorConnectionString)
            .Options;
        await using var context = new PostgresDbContext(options);
        var processor = new PhotoCleanupProcessor(new PostgresPhotoCleanupStore(context), s3Cleanup, configuration, TimeProvider.System);
        return await processor.ProcessOwnerAsync(ownerId, batchSize, leaseDuration, cancellationToken);
    }
}
