using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
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
    private const string MigrateCommand = "migrate";
    private const string MigrationStatusCommand = "migration-status";
    private const string SeedCommand = "seed";
    private const string ReviewCatalogDryRunCommand = "review-catalog-dry-run";
    private const string ReviewCatalogApplyCommand = "review-catalog-apply";
    private const string CleanupPhotosCommand = "cleanup-photos";
    private const string ConfirmationSwitch = "--confirm";
    private const int Sha256HexLength = 64;
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

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var command, out var manifestPath, out var expectedSha256))
        {
            Console.Error.WriteLine("Usage: DbMaintenance migration-status | migrate --confirm | seed <manifest-path> <expected-sha256> --confirm | review-catalog-dry-run <manifest-path> <expected-sha256> | review-catalog-apply <manifest-path> <expected-sha256> --confirm | cleanup-photos <page-size> <page-count> <owner-batch-size> <lease-minutes>");
            return InvalidUsageExitCode;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LambdaFunctionEnvironmentVariable)))
        {
            Console.Error.WriteLine("Database maintenance is forbidden in AWS Lambda.");
            return InvalidUsageExitCode;
        }

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        var isCleanupCommand = args.Length > 0 && args[0] == CleanupPhotosCommand;
        if (!isCleanupCommand && string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("ConnectionStrings__PostgreSql is required in the protected runner environment.");
            return InvalidUsageExitCode;
        }

        try
        {
            if (command == CleanupPhotosCommand)
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

            if (command is SeedCommand or ReviewCatalogDryRunCommand or ReviewCatalogApplyCommand)
            {
                manifestPath = ResolvePath(manifestPath!);
                if (!await ManifestMatchesAsync(manifestPath, expectedSha256!))
                    throw new InvalidOperationException("Manifest SHA-256 does not match the supplied checksum.");
            }

            var options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(connection.ConnectionString, provider =>
                    provider.MigrationsAssembly(typeof(PostgresDbContext).Assembly.GetName().Name))
                .Options;
            await using var context = new PostgresDbContext(options);

            if (command == MigrationStatusCommand)
            {
                var applied = await context.Database.GetAppliedMigrationsAsync();
                var pending = await context.Database.GetPendingMigrationsAsync();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Applied = applied, Pending = pending }));
                return 0;
            }

            if (command == MigrateCommand)
            {
                await EnsureRolesExistAsync(context);
                await context.Database.MigrateAsync();
                Console.WriteLine("Schema migration completed.");
                return 0;
            }

            if (command == ReviewCatalogDryRunCommand)
            {
                var importer = new PostgresReviewedExerciseCatalogImporterV020(context);
                var dryRun = await importer.DryRunAsync(manifestPath!);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(dryRun));
                return dryRun.CanImport ? 0 : 1;
            }

            if ((await context.Database.GetPendingMigrationsAsync()).Any())
                throw new InvalidOperationException("Apply schema migrations before catalog writes.");

            if (command == ReviewCatalogApplyCommand)
            {
                var importer = new PostgresReviewedExerciseCatalogImporterV020(context);
                var dryRun = await importer.DryRunAsync(manifestPath!);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(dryRun));
                if (!dryRun.CanImport)
                    return 1;
                await importer.ApplyAsync(manifestPath!);
                Console.WriteLine("Exercise review metadata applied with separate owner restriction assertion; existing contraindications remain and automatic substitutions remain disabled.");
                return 0;
            }

            var summary = await PostgresCanonicalCatalogSeeder.IngestFromManifestAsync(context, manifestPath!);
            Console.WriteLine($"Canonical seed completed: {summary.TotalManifestRows} manifest rows, SHA-256 {summary.ManifestIntegritySha256}.");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Provider exceptions may include connection strings or object names. Keep runner logs secret-free.
            Console.Error.WriteLine($"Database maintenance failed ({exception.GetType().Name}: {exception.Message}). Check the runner's connection, certificate, role, and manifest settings.");
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

    public static bool TryParse(
        string[] args,
        out string? command,
        out string? manifestPath,
        out string? expectedSha256)
    {
        command = null;
        manifestPath = null;
        expectedSha256 = null;

        if (args is [MigrationStatusCommand])
        {
            command = MigrationStatusCommand;
            return true;
        }

        if (args is [MigrateCommand, ConfirmationSwitch])
        {
            command = MigrateCommand;
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

        if (args is [ReviewCatalogDryRunCommand, var dryRunPath, var dryRunChecksum] &&
            !string.IsNullOrWhiteSpace(dryRunPath) && dryRunChecksum.Length == Sha256HexLength &&
            dryRunChecksum.All(Uri.IsHexDigit))
        {
            command = ReviewCatalogDryRunCommand;
            manifestPath = dryRunPath;
            expectedSha256 = dryRunChecksum;
            return true;
        }

        if (args is [ReviewCatalogApplyCommand, var applyPath, var applyChecksum, ConfirmationSwitch] &&
            !string.IsNullOrWhiteSpace(applyPath) && applyChecksum.Length == Sha256HexLength &&
            applyChecksum.All(Uri.IsHexDigit))
        {
            command = ReviewCatalogApplyCommand;
            manifestPath = applyPath;
            expectedSha256 = applyChecksum;
            return true;
        }

        if (args is not [SeedCommand, var path, var checksum, ConfirmationSwitch] ||
            string.IsNullOrWhiteSpace(path) ||
            checksum.Length != Sha256HexLength ||
            !checksum.All(Uri.IsHexDigit))
            return false;

        command = SeedCommand;
        manifestPath = path;
        expectedSha256 = checksum;
        return true;
    }

    private static string ResolvePath(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), path),
            Path.Combine(AppContext.BaseDirectory, path),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", path)
        };
        return candidates.FirstOrDefault(File.Exists) ?? path;
    }

    private static async Task<bool> ManifestMatchesAsync(string manifestPath, string expectedSha256)
    {
        if (!File.Exists(manifestPath))
            return false;

        var manifest = await File.ReadAllBytesAsync(manifestPath);
        var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(manifest));
        return string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureRolesExistAsync(PostgresDbContext context)
    {
        const string bootstrapRolesSql = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
                    CREATE ROLE musclecuties_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                    CREATE ROLE musclecuties_app NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_auth') THEN
                    CREATE ROLE musclecuties_auth NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch') THEN
                    CREATE ROLE musclecuties_cleanup_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_discovery_owner') THEN
                    CREATE ROLE musclecuties_cleanup_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
                END IF;
            END $$;
            """;
        await context.Database.ExecuteSqlRawAsync(bootstrapRolesSql);
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
