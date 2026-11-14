using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Discovery-time guard for the database-backed integration suite. A missing fixture must
/// produce one clear skip reason on every affected test, not a cascade of connection errors.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresTestEnvironment.IsConfigured) return;
        Skip = PostgresTestEnvironment.SkipReason;
    }
}

/// <inheritdoc cref="PostgresFactAttribute"/>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (PostgresTestEnvironment.IsConfigured) return;
        Skip = PostgresTestEnvironment.SkipReason;
    }
}

internal static class PostgresTestEnvironment
{
    public const string MarkerVariable = "MC_INTEGRATION_POSTGRES";

    public static bool IsConfigured =>
        string.Equals(Environment.GetEnvironmentVariable(MarkerVariable), "1", StringComparison.Ordinal);

    public const string SkipReason =
        "PostgreSQL integration fixtures are not configured. Run " +
        "`./scripts/prepare-integration-postgres.sh`, source `.env.integration`, and rerun. " +
        "See docs/INTEGRATION_POSTGRES.md.";

    public static string RequireConnection(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        throw new InvalidOperationException(
            $"{variable} is not set. Run `./scripts/prepare-integration-postgres.sh`, source " +
            "`.env.integration`, and rerun. See docs/INTEGRATION_POSTGRES.md.");
    }
}
