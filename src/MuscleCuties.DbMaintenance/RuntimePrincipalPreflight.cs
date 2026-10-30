using System.Data;
using Npgsql;

namespace MuscleCuties.DbMaintenance.Schema;

/// <summary>
/// Read-only candidate-login check. This is catalog evidence, not a substitute for
/// authenticated API and tenant-isolation tests before changing a Lambda secret.
/// </summary>
public static class RuntimePrincipalPreflight
{
    private const string AppRole = "musclecuties_app";
    private const string MasterLogin = "mc_admin";

    public static async Task<RuntimePrincipalReport> EvaluateAsync(
        string candidateConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateConnectionString);
        var builder = new NpgsqlConnectionStringBuilder(candidateConnectionString);
        if (builder.Host is not ("127.0.0.1" or "localhost") && builder.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Remote candidate connections require SSL Mode=VerifyFull.");

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);

        const string sql = """
            SELECT session_user::text, current_user::text,
                   login.rolcanlogin, login.rolsuper, login.rolbypassrls,
                   login.rolcreatedb, login.rolcreaterole,
                   pg_has_role(session_user, 'musclecuties_app', 'MEMBER'),
                   pg_has_role(session_user, 'musclecuties_migrator', 'MEMBER'),
                   pg_has_role(session_user, 'musclecuties_workout_dispatch', 'MEMBER'),
                   pg_has_role(session_user, 'musclecuties_workout_discovery_owner', 'MEMBER'),
                   (SELECT count(*) FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace
                    WHERE n.nspname IN ('app', 'catalog', 'ops') AND c.relkind IN ('r', 'p', 'S')
                      AND c.relowner IN (login.oid,
                          (SELECT oid FROM pg_roles WHERE rolname = 'musclecuties_app'))),
                   (SELECT bool_or(has_schema_privilege(session_user, schema_name, 'CREATE') OR
                                       has_schema_privilege(current_user, schema_name, 'CREATE'))
                    FROM unnest(ARRAY['app', 'catalog', 'ops']) AS schema_name),
                   has_table_privilege(current_user, 'app.users', 'SELECT'),
                   (SELECT bool_and(has_table_privilege(current_user, 'app.email_challenges', privilege))
                    FROM unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE']) AS privilege),
                   (SELECT bool_and(has_table_privilege(current_user, 'app.email_challenge_sessions', privilege))
                    FROM unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE']) AS privilege),
                   has_table_privilege(current_user, 'public."__EFMigrationsHistory"', 'SELECT')
            FROM pg_roles AS login WHERE login.rolname = session_user
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Candidate login metadata is unavailable.");

        var observation = new RuntimePrincipalObservation(
            reader.GetString(0), reader.GetString(1), reader.GetBoolean(2),
            reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5),
            reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8),
            reader.GetBoolean(9), reader.GetBoolean(10), reader.GetInt64(11),
            reader.GetBoolean(12), reader.GetBoolean(13), reader.GetBoolean(14),
            reader.GetBoolean(15), reader.GetBoolean(16));
        return Assess(observation);
    }

    public static RuntimePrincipalReport Assess(RuntimePrincipalObservation observation)
    {
        var failures = new List<string>();
        if (string.Equals(observation.SessionUser, MasterLogin, StringComparison.Ordinal) ||
            string.Equals(observation.SessionUser, AppRole, StringComparison.Ordinal))
            failures.Add("CANDIDATE_LOGIN_NOT_DEDICATED");
        if (!string.Equals(observation.EffectiveUser, AppRole, StringComparison.Ordinal))
            failures.Add("EFFECTIVE_ROLE_NOT_APP");
        if (!observation.CanLogin || observation.IsSuperuser || observation.BypassesRls ||
            observation.CanCreateDatabase || observation.CanCreateRole)
            failures.Add("CANDIDATE_LOGIN_PRIVILEGED");
        if (!observation.MemberOfApp || observation.MemberOfMigrator || observation.MemberOfWorkoutDispatch ||
            observation.MemberOfWorkoutDiscoveryOwner)
            failures.Add("CANDIDATE_ROLE_MEMBERSHIP_INVALID");
        if (observation.OwnedObjectCount != 0 || observation.CanCreateInAppSchema)
            failures.Add("CANDIDATE_HAS_DDL_ACCESS");
        if (!observation.CanReadUsers)
            failures.Add("USER_LOOKUP_ACCESS_MISSING");
        if (!observation.CanUseEmailChallenges || !observation.CanUseEmailChallengeSessions)
            failures.Add("AUTH_TABLE_ACCESS_MISSING");
        if (!observation.CanReadMigrationHistory)
            failures.Add("READINESS_LEDGER_ACCESS_MISSING");

        return new RuntimePrincipalReport(
            failures.Count == 0 ? "catalog-compatible" : "blocked",
            failures.Count == 0,
            failures);
    }
}

public sealed record RuntimePrincipalObservation(
    string SessionUser,
    string EffectiveUser,
    bool CanLogin,
    bool IsSuperuser,
    bool BypassesRls,
    bool CanCreateDatabase,
    bool CanCreateRole,
    bool MemberOfApp,
    bool MemberOfMigrator,
    bool MemberOfWorkoutDispatch,
    bool MemberOfWorkoutDiscoveryOwner,
    long OwnedObjectCount,
    bool CanCreateInAppSchema,
    bool CanReadUsers,
    bool CanUseEmailChallenges,
    bool CanUseEmailChallengeSessions,
    bool CanReadMigrationHistory);

public sealed record RuntimePrincipalReport(
    string Status,
    bool CatalogCompatible,
    IReadOnlyList<string> FailureCodes);
