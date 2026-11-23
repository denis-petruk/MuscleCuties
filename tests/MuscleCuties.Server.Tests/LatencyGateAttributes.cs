using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// Discovery-time guard for the all-route latency gate. The gate measures real PostgreSQL work, so it
/// needs a database; without one it must report one clear skip instead of failing the ordinary suite.
/// This mirrors <c>PostgresFactAttribute</c> in the integration suite.
/// </summary>
public sealed class LatencyGateFactAttribute : FactAttribute
{
    public LatencyGateFactAttribute()
    {
        if (LatencyGateEnvironment.IsConfigured) return;
        Skip = LatencyGateEnvironment.SkipReason;
    }
}

/// <summary>
/// The latency gate runs when <c>MC_LATENCY_POSTGRES</c> holds a connection string. The standard CI
/// invocation excludes this test by name and the dedicated latency job supplies the variable.
/// </summary>
internal static class LatencyGateEnvironment
{
    public const string ConnectionVariable = "MC_LATENCY_POSTGRES";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    public const string SkipReason =
        "The all-route latency gate needs a disposable PostgreSQL fixture. Set MC_LATENCY_POSTGRES to a " +
        "connection string and rerun. The synthetic unreachable-DB fallback is intentionally not measured.";

    public static string RequireConnection()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        throw new InvalidOperationException($"{ConnectionVariable} is not set. {SkipReason}");
    }
}
