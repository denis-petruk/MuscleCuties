using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

namespace MuscleCuties.DbMaintenance.Schema;

/// <summary>Stable reconciliation failure classes. Reconciliation fails closed on every uncertainty.</summary>
public static class ReconciliationFailureCodes
{
    public const string NotConfirmed = "RECONCILIATION_NOT_CONFIRMED";
    public const string NotApproved = "RECONCILIATION_NOT_APPROVED";
    public const string ManifestHashMismatch = "RECONCILIATION_MANIFEST_HASH_MISMATCH";
    public const string ManifestInvalid = "RECONCILIATION_MANIFEST_INVALID";
    public const string NotRequired = "RECONCILIATION_NOT_REQUIRED";
    public const string PreflightUnverified = "RECONCILIATION_PREFLIGHT_UNVERIFIED";
    public const string TargetMismatch = "RECONCILIATION_TARGET_MISMATCH";
    public const string HistoryModeMismatch = "RECONCILIATION_HISTORY_MODE_MISMATCH";
    public const string OperationHashMismatch = "RECONCILIATION_OPERATION_HASH_MISMATCH";
    public const string OperationPathRejected = "RECONCILIATION_OPERATION_PATH_REJECTED";
    public const string DependencyInvalid = "RECONCILIATION_DEPENDENCY_INVALID";
    public const string LockUnavailable = "RECONCILIATION_LOCK_UNAVAILABLE";
    public const string OperationFailed = "RECONCILIATION_OPERATION_FAILED";
    public const string PartialState = "RECONCILIATION_PARTIAL_STATE";
    public const string PostconditionFailed = "RECONCILIATION_POSTCONDITION_FAILED";
}

public sealed class ReconciliationManifest
{
    [JsonPropertyName("contractVersion")] public string ContractVersion { get; set; } = string.Empty;
    [JsonPropertyName("mode")] public string Mode { get; set; } = string.Empty;
    [JsonPropertyName("approved")] public bool Approved { get; set; }
    [JsonPropertyName("approvedBy")] public string? ApprovedBy { get; set; }
    [JsonPropertyName("approvalReference")] public string? ApprovalReference { get; set; }
    [JsonPropertyName("targetFingerprintSha256")] public string TargetFingerprintSha256 { get; set; } = string.Empty;
    [JsonPropertyName("expectedHistoryMode")] public string ExpectedHistoryMode { get; set; } = string.Empty;
    [JsonPropertyName("operations")] public List<ReconciliationOperation> Operations { get; set; } = [];
}

public sealed class ReconciliationOperation
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("sqlFile")] public string SqlFile { get; set; } = string.Empty;
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    [JsonPropertyName("transaction")] public string Transaction { get; set; } = "transactional";
    [JsonPropertyName("dependsOn")] public List<string> DependsOn { get; set; } = [];
}

public sealed record ReconciliationStepResult(string Id, string Transaction, bool Committed, string? FailureCode);

public sealed record ReconciliationOutcome(
    string Status,
    IReadOnlyList<string> FailureCodes,
    IReadOnlyList<ReconciliationStepResult> Steps,
    string PostReconciliationStatus,
    string PostFingerprintSha256 = "");

/// <summary>
/// Approval-gated reconciliation. No approved manifest ships with the repository, so
/// <c>schema-reconcile</c> always fails closed until an owner approves exact operations, an exact
/// target fingerprint and a migration-history policy. This is deliberately not an arbitrary SQL executor.
/// </summary>
public static class SchemaReconciliation
{
    public const string ReconciliationDirectory = "Schema/Reconciliation";
    public const int AdvisoryLockClass = 0x4D435343;
    public const int AdvisoryLockKey = 0x53434845;
    public const string ModeSqlReconciled = "sql-reconciled";
    public const string ModeEfMigrationChain = "ef-migration-chain";
    public const string Transactional = "transactional";
    public const string NonTransactional = "nontransactional";
    public const string StatusReconciled = "reconciled";
    public const string StatusNotRequired = "not-required";
    public const string StatusFailed = "failed";
    public const string StatusPostconditionFailed = "postcondition-failed";

    private const int Sha256HexLength = 64;
    private const int CommandTimeoutSeconds = 60;
    private const string LockTimeout = "5s";
    private const string StatementTimeout = "60s";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockPollInterval = TimeSpan.FromMilliseconds(250);

    public static ReconciliationOutcome Refuse(string failureCode) =>
        new(StatusFailed, [failureCode], [], "not-evaluated");

    public static async Task<ReconciliationOutcome> ExecuteAsync(
        string connectionString,
        string manifestPath,
        string expectedManifestSha256,
        SchemaRequirements requirements,
        string repositoryRoot,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
            return Refuse(ReconciliationFailureCodes.NotConfirmed);

        if (!File.Exists(manifestPath))
            return Refuse(ReconciliationFailureCodes.ManifestInvalid);

        var actualManifestSha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(manifestPath, cancellationToken)));
        if (!string.Equals(actualManifestSha256, expectedManifestSha256, StringComparison.OrdinalIgnoreCase))
            return Refuse(ReconciliationFailureCodes.ManifestHashMismatch);

        ReconciliationManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ReconciliationManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true }) ?? new ReconciliationManifest();
        }
        catch (JsonException)
        {
            return Refuse(ReconciliationFailureCodes.ManifestInvalid);
        }

        var validation = ValidateManifest(manifest, requirements);
        if (validation is not null)
            return Refuse(validation);

        var operations = new List<(ReconciliationOperation Operation, string Path, string Sql)>();
        foreach (var operation in manifest.Operations)
        {
            if (!TryResolveOperationPath(repositoryRoot, operation.SqlFile, out var path))
                return Refuse(ReconciliationFailureCodes.OperationPathRejected);

            if (!File.Exists(path))
                return Refuse(ReconciliationFailureCodes.OperationPathRejected);

            var sql = await File.ReadAllTextAsync(path, cancellationToken);
            var actualSha = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql)));
            if (!string.Equals(actualSha, operation.Sha256, StringComparison.OrdinalIgnoreCase))
                return Refuse(ReconciliationFailureCodes.OperationHashMismatch);

            operations.Add((operation, path, sql));
        }

        var ordered = OrderByDependency(operations.Select(entry => entry.Operation).ToList());
        if (ordered is null)
            return Refuse(ReconciliationFailureCodes.DependencyInvalid);
        var byId = operations.ToDictionary(entry => entry.Operation.Id, StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var lockLease = await TryAcquireAdvisoryLockAsync(connection, cancellationToken);
        if (lockLease is null)
            return Refuse(ReconciliationFailureCodes.LockUnavailable);

        var preflight = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements, cancellationToken);
        if (preflight.Status == SchemaCompatibilityStatus.Compatible)
            return new ReconciliationOutcome(StatusNotRequired, [ReconciliationFailureCodes.NotRequired], [], preflight.Status, preflight.DatabaseFingerprintSha256);
        if (preflight.Status != SchemaCompatibilityStatus.Incompatible)
            return new ReconciliationOutcome(StatusFailed, [ReconciliationFailureCodes.PreflightUnverified], [], preflight.Status);
        if (!string.Equals(preflight.DatabaseFingerprintSha256, manifest.TargetFingerprintSha256, StringComparison.OrdinalIgnoreCase))
            return new ReconciliationOutcome(StatusFailed, [ReconciliationFailureCodes.TargetMismatch], [], preflight.Status,
                preflight.DatabaseFingerprintSha256);
        if (!string.Equals(preflight.MigrationHistoryMode, manifest.ExpectedHistoryMode, StringComparison.Ordinal))
            return new ReconciliationOutcome(StatusFailed, [ReconciliationFailureCodes.HistoryModeMismatch], [], preflight.Status);

        var steps = new List<ReconciliationStepResult>();
        foreach (var operation in ordered)
        {
            var entry = byId[operation.Id];
            try
            {
                await RunOperationAsync(connection, operation, entry.Sql, cancellationToken);
                steps.Add(new ReconciliationStepResult(operation.Id, operation.Transaction, true, null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                steps.Add(new ReconciliationStepResult(operation.Id, operation.Transaction, false,
                    ReconciliationFailureCodes.OperationFailed));
                var failures = new List<string> { ReconciliationFailureCodes.OperationFailed };
                if (!string.Equals(operation.Transaction, Transactional, StringComparison.Ordinal) || steps.Count > 1)
                    failures.Add(ReconciliationFailureCodes.PartialState);
                return new ReconciliationOutcome(StatusFailed, failures, steps, "not-evaluated");
            }
        }

        var postflight = await DatabaseSchemaCompatibility.EvaluateAsync(connectionString, requirements, cancellationToken);
        if (postflight.Status == SchemaCompatibilityStatus.Compatible)
            return new ReconciliationOutcome(StatusReconciled, [], steps, postflight.Status, postflight.DatabaseFingerprintSha256);

        return new ReconciliationOutcome(StatusPostconditionFailed,
            postflight.FailureCodes.Append(ReconciliationFailureCodes.PostconditionFailed).Distinct(StringComparer.Ordinal).ToArray(),
            steps, postflight.Status, postflight.DatabaseFingerprintSha256);
    }

    /// <summary>
    /// Executes one reviewed operation. Transactional operations roll back atomically on failure;
    /// nontransactional operations are classified explicitly by the approved manifest.
    /// </summary>
    public static async Task RunOperationAsync(
        NpgsqlConnection connection,
        ReconciliationOperation operation,
        string sql,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(operation.Transaction, Transactional, StringComparison.Ordinal))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction)
                {
                    CommandTimeout = CommandTimeoutSeconds
                };
                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return;
        }

        await using (var command = new NpgsqlCommand(sql, connection)
                     {
                         CommandTimeout = CommandTimeoutSeconds
                     })
            await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts one session-held advisory lock with a bounded wait. The lock stays held for the whole
    /// critical section on the same session; a crashed session releases it with the connection.
    /// </summary>
    public static async Task<IDisposable?> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + LockWait;
        while (true)
        {
            await using (var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@class, @key)", connection)
                         {
                             CommandTimeout = CommandTimeoutSeconds
                         })
            {
                command.Parameters.AddWithValue("class", AdvisoryLockClass);
                command.Parameters.AddWithValue("key", AdvisoryLockKey);
                var acquired = await command.ExecuteScalarAsync(cancellationToken);
                if (acquired is true)
                {
                    await using var settings = new NpgsqlCommand(
                        $"SET lock_timeout = '{LockTimeout}'; " +
                        $"SET statement_timeout = '{StatementTimeout}'", connection);
                    await settings.ExecuteNonQueryAsync(cancellationToken);
                    return new AdvisoryLockHandle(connection);
                }
            }

            if (DateTimeOffset.UtcNow >= deadline)
                return null;
            await Task.Delay(LockPollInterval, cancellationToken);
        }
    }

    public static async Task<bool> ReleaseAdvisoryLockAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@class, @key)", connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        command.Parameters.AddWithValue("class", AdvisoryLockClass);
        command.Parameters.AddWithValue("key", AdvisoryLockKey);
        return await command.ExecuteScalarAsync() is true;
    }

    private static string? ValidateManifest(ReconciliationManifest manifest, SchemaRequirements requirements)
    {
        if (!string.Equals(manifest.ContractVersion, requirements.ContractVersion, StringComparison.Ordinal))
            return ReconciliationFailureCodes.ManifestInvalid;
        if (!manifest.Approved || string.IsNullOrWhiteSpace(manifest.ApprovedBy) ||
            string.IsNullOrWhiteSpace(manifest.ApprovalReference))
            return ReconciliationFailureCodes.NotApproved;
        if (!string.Equals(manifest.Mode, ModeSqlReconciled, StringComparison.Ordinal))
            return ReconciliationFailureCodes.NotApproved;
        if (!IsSha256(manifest.TargetFingerprintSha256) || string.IsNullOrWhiteSpace(manifest.ExpectedHistoryMode))
            return ReconciliationFailureCodes.ManifestInvalid;
        if (manifest.Operations.Count == 0)
            return ReconciliationFailureCodes.ManifestInvalid;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in manifest.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Id) || !ids.Add(operation.Id) ||
                string.IsNullOrWhiteSpace(operation.SqlFile) || !files.Add(operation.SqlFile) ||
                !IsSha256(operation.Sha256) ||
                operation.Transaction is not (Transactional or NonTransactional))
                return ReconciliationFailureCodes.ManifestInvalid;
        }

        if (manifest.Operations.Any(operation => operation.DependsOn.Any(dependency => !ids.Contains(dependency))))
            return ReconciliationFailureCodes.DependencyInvalid;

        if (OrderByDependency(manifest.Operations) is null)
            return ReconciliationFailureCodes.DependencyInvalid;

        return null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: Sha256HexLength } && value.All(Uri.IsHexDigit);

    private static bool TryResolveOperationPath(string repositoryRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains("..", StringComparison.Ordinal))
            return false;

        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.StartsWith($"{ReconciliationDirectory}/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".sql", StringComparison.Ordinal) ||
            !Regex.IsMatch(normalized, "^[A-Za-z0-9_./-]+$"))
            return false;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(repositoryRoot, normalized)),
            Path.GetFullPath(Path.Combine(repositoryRoot, "src", "MuscleCuties.DbMaintenance", normalized))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                fullPath = candidate;
                return true;
            }
        }

        fullPath = candidates[0];
        var reconciliationRoot = Path.GetFullPath(Path.Combine(repositoryRoot, ReconciliationDirectory));
        return fullPath.StartsWith(reconciliationRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static List<ReconciliationOperation>? OrderByDependency(List<ReconciliationOperation> operations)
    {
        var byId = operations.ToDictionary(operation => operation.Id, StringComparer.Ordinal);
        var ordered = new List<ReconciliationOperation>();
        var remaining = new Dictionary<string, ReconciliationOperation>(byId, StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var ready = remaining.Values
                .Where(operation => operation.DependsOn.All(dependency => ordered.Any(done => done.Id == dependency)))
                .OrderBy(operation => operation.Id, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
                return null;
            foreach (var operation in ready)
            {
                ordered.Add(operation);
                remaining.Remove(operation.Id);
            }
        }

        return ordered;
    }

    private sealed class AdvisoryLockHandle(NpgsqlConnection connection) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@class, @key)", connection)
                {
                    CommandTimeout = CommandTimeoutSeconds
                };
                command.Parameters.AddWithValue("class", AdvisoryLockClass);
                command.Parameters.AddWithValue("key", AdvisoryLockKey);
                command.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // The session or database is already gone; the lock is released with it.
            }
        }
    }
}
