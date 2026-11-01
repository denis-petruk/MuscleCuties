using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.DbMaintenance;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

return await DatabaseMaintenance.RunAsync(args);

public static class DatabaseMaintenance
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__PostgreSql";
    private const string CleanupDispatchConnectionStringEnvironmentVariable = "ConnectionStrings__PhotoCleanupDispatch";
    private const string CleanupProcessorConnectionStringEnvironmentVariable = "ConnectionStrings__PhotoCleanupProcessor";
    private const string LambdaFunctionEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";
    private const string DbInitCommand = "dbinit";
    private const string DbCheckCommand = "dbcheck";
    private const string SeedCommand = "seed";
    private const string SessionLockRunnerCommand = "session-lock-runner";
    private const string CleanupPhotosCommand = "cleanup-photos";
    private const string CompatibleStatus = "compatible";
    private const string IncompatibleStatus = "incompatible";
    private const string ContractVersion = "1.0.0";
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

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var command))
        {
            Console.Error.WriteLine(
                "Usage: DbMaintenance dbinit | dbcheck | seed | session-lock-runner -- <child-command> [args...] | " +
                "cleanup-photos <page-size> <page-count> <owner-batch-size> <lease-minutes>");
            return InvalidUsageExitCode;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LambdaFunctionEnvironmentVariable)))
        {
            Console.Error.WriteLine("Database maintenance is forbidden in AWS Lambda.");
            return InvalidUsageExitCode;
        }

        if (command == SessionLockRunnerCommand)
        {
            try
            {
                return await RunSessionLockRunnerAsync(args);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Session lock runner failed ({exception.GetType().Name}).");
                return 1;
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
            var isLoopback = connection.Host is "127.0.0.1" or "localhost";
            if (!isLoopback && connection.SslMode != SslMode.VerifyFull)
                throw new InvalidOperationException("Database maintenance requires SSL Mode=VerifyFull.");

            if (!isLoopback)
            {
                if (string.IsNullOrWhiteSpace(connection.RootCertificate))
                {
                    var candidates = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "certs", "global-bundle.pem"),
                        Path.Combine(AppContext.BaseDirectory, "certs", "global-bundle.pem"),
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "certs", "global-bundle.pem")
                    };
                    var foundCert = candidates.FirstOrDefault(File.Exists);
                    if (foundCert is not null)
                    {
                        connection.RootCertificate = foundCert;
                    }
                }
                else if (!File.Exists(connection.RootCertificate))
                {
                    var candidates = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), connection.RootCertificate),
                        Path.Combine(AppContext.BaseDirectory, connection.RootCertificate),
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", connection.RootCertificate)
                    };
                    var foundCert = candidates.FirstOrDefault(File.Exists);
                    if (foundCert is not null)
                    {
                        connection.RootCertificate = foundCert;
                    }
                }
            }

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
            // Provider exceptions may include connection strings or object names. Keep runner logs secret-free.
            Console.Error.WriteLine($"Database maintenance failed ({exception.GetType().Name}). Check the runner's connection, certificate and role settings.");
            return 1;
        }
    }

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

        if (args is [DbInitCommand] or [DbCheckCommand] or [SeedCommand])
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

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, ResultJsonOptions));

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
        var isLoopback = connection.Host is "127.0.0.1" or "localhost";
        if (!isLoopback && connection.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("session-lock-runner requires SSL Mode=VerifyFull.");

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

        ConsoleCancelEventHandler cancelHandler = (s, e) =>
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

internal sealed class LocalPhotoCleanupOwnerProcessorRunner(
    string processorConnectionString,
    MuscleCuties.UseCases.Features.Profile.PhotoVault.IS3PhotoObjectCleanup s3Cleanup,
    IConfiguration configuration) : IPhotoCleanupOwnerProcessorRunner
{
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
